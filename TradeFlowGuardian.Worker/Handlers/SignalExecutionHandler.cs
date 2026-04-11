using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
using Prometheus;
using StackExchange.Redis;
using TradeFlowGuardian.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace TradeFlowGuardian.Worker.Handlers;

/// <summary>
/// Handles a single trade signal end-to-end:
/// 1. Run filters
/// 2. Check for existing position (no pyramiding)
/// 3. Size position
/// 4. Calculate SL/TP
/// 5. Submit order to OANDA
/// </summary>
public class SignalExecutionHandler(
    ISignalFilter filter,
    IOandaClient oanda,
    IPositionSizer sizer,
    IPositionCache positionCache,
    IOptions<RiskConfig> risk,
    IConnectionMultiplexer redis,
    ILogger<SignalExecutionHandler> logger)
{
    private readonly RiskConfig _risk = risk.Value;
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task HandleAsync(TradeSignal signal, CancellationToken ct)
    {
        using var scope = logger.BeginScope(
            "Signal {Direction} {Instrument} {Key}",
            signal.Direction, signal.Instrument, signal.IdempotencyKey);

        try
        {
            await ProcessSignalInternalAsync(signal, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancellation reached here means shutdown fired during pre-order checks
            // (filters, balance fetch, position sizing). Order placement and close
            // use CancellationToken.None so they are never interrupted once started.
            logger.LogWarning(
                "Signal processing interrupted by shutdown for {Instrument} — was in pre-order checks, no order was submitted",
                signal.Instrument);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing signal for {Instrument}: {Message}", 
                signal.Instrument, ex.Message);
            throw; // Re-throw to let the worker handle it/log it as unhandled
        }
    }

    private async Task ProcessSignalInternalAsync(TradeSignal signal, CancellationToken ct)
    {
        // ── Idempotency check ─────────────────────────────────────────────────
        if (signal.IdempotencyKey is not null)
        {
            var key = $"idempotency:signal:{signal.IdempotencyKey}";
            // Set with 24h TTL if it doesn't exist
            try 
            {
                var set = await _db.StringSetAsync(key, (RedisValue)"1", TimeSpan.FromHours(24), When.NotExists);
                if (!set)
                {
                    logger.LogWarning("Duplicate signal ignored: {Key}", signal.IdempotencyKey);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking idempotency in Redis for {Key}. Error: {Message}", signal.IdempotencyKey, ex.Message);
                // Fail-closed
                return;
            }
        }

        TradeMetrics.SignalsReceived.Inc();

        // ── Handle Close signal ───────────────────────────────────────────────
        if (signal.Direction == SignalDirection.Close)
        {
            // CancellationToken.None — must not abort a close request once sent.
            var closeResult = await oanda.ClosePositionAsync(signal.Instrument, CancellationToken.None);
            if (closeResult.Success)
            {
                await positionCache.ClearAsync(signal.Instrument, CancellationToken.None);
                logger.LogInformation("Successfully closed position for {Instrument}: {Message}",
                    signal.Instrument, closeResult.Message);
            }
            else
            {
                logger.LogError("Failed to close position for {Instrument}: {Message}",
                    signal.Instrument, closeResult.Message);
            }
            return;
        }

        // ── Run signal filters ────────────────────────────────────────────────
        var filterResult = await filter.EvaluateAsync(signal, ct);
        if (!filterResult.Allowed)
        {
            logger.LogWarning("Signal for {Instrument} blocked by filter. Reason: {Reason}",
                signal.Instrument, filterResult.Reason);
            TradeMetrics.SignalsFiltered.WithLabels(filterResult.Label).Inc();
            return;
        }

        // ── No pyramiding — check for existing position (cache → OANDA fallback) ──
        var (cached, cachedUnits) = await positionCache.GetAsync(signal.Instrument, ct);
        decimal? existingUnits;
        if (cached)
        {
            existingUnits = cachedUnits;
            logger.LogDebug("Position cache hit for {Instrument}: {Units} units", signal.Instrument, cachedUnits);
        }
        else
        {
            existingUnits = await oanda.GetOpenPositionUnitsAsync(signal.Instrument, ct);
            if (existingUnits is not null)
                await positionCache.SetAsync(signal.Instrument, existingUnits.Value, ct);
        }

        if (existingUnits is not null)
        {
            logger.LogWarning(
                "Signal skipped: Position already open on {Instrument} ({Units} units). No pyramiding allowed.",
                signal.Instrument, existingUnits);
            return;
        }

        // ── Position sizing ───────────────────────────────────────────────────
        var balance = await oanda.GetAccountBalanceAsync(ct);
        if (balance <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Could not fetch account balance or balance is non-positive ({Balance:C})",
                signal.Instrument, balance);
            return;
        }

        TradeMetrics.AccountBalance.Set((double)balance);

        var units = await sizer.CalculateUnitsAsync(signal, balance, ct);
        if (units <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Position size calculated as {Units} units. Check ATR ({Atr}) and risk configuration (Risk%: {RiskPercent}%, Balance: {Balance:C})",
                signal.Instrument, units, signal.Atr, signal.RiskPercent > 0 ? signal.RiskPercent : _risk.DefaultRiskPercent, balance);
            return;
        }

        // ── SL / TP calculation ───────────────────────────────────────────────
        var stopDistance   = signal.Atr * _risk.AtrStopMultiplier;
        var targetDistance = signal.Atr * _risk.AtrTargetMultiplier;

        var isJpy = signal.Instrument.Contains("JPY");
        var priceFmt = isJpy ? "F3" : "F5";

        decimal stopLoss, takeProfit;
        if (signal.Direction == SignalDirection.Long)
        {
            stopLoss   = signal.Price - stopDistance;
            takeProfit = signal.Price + targetDistance;
        }
        else
        {
            stopLoss   = signal.Price + stopDistance;
            takeProfit = signal.Price - targetDistance;
        }

        // ── SL/TP Sanity Check ────────────────────────────────────────────────
        if (takeProfit <= 0 || stopLoss <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Invalid SL/TP calculated (SL={SL}, TP={TP}). Check ATR ({Atr})",
                signal.Instrument, stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt), signal.Atr);
            return;
        }

        if (signal.Direction == SignalDirection.Long && takeProfit <= signal.Price)
        {
            logger.LogError("Aborting signal for {Instrument}: TP ({TP}) is not above entry price ({Price}) for LONG",
                signal.Instrument, takeProfit.ToString(priceFmt), signal.Price.ToString(priceFmt));
            return;
        }

        if (signal.Direction == SignalDirection.Short && takeProfit >= signal.Price)
        {
            logger.LogError("Aborting signal for {Instrument}: TP ({TP}) is not below entry price ({Price}) for SHORT",
                signal.Instrument, takeProfit.ToString(priceFmt), signal.Price.ToString(priceFmt));
            return;
        }

        logger.LogInformation(
            "Executing {Direction} order for {Instrument} | Units={Units} | AccountBalance={Balance:C} | Entry={Price} | SL={SL} (dist: {StopDist}) | TP={TP} (dist: {TargetDist})",
            signal.Direction, signal.Instrument, units, balance, signal.Price, stopLoss.ToString(priceFmt), stopDistance.ToString(priceFmt), takeProfit.ToString(priceFmt), targetDistance.ToString(priceFmt));

        // ── Place order ───────────────────────────────────────────────────────
        // CancellationToken.None for both calls: once we decide to trade, we must
        // see it through. Aborting PlaceMarketOrderAsync mid-flight leaves the order
        // state at OANDA unknown. Aborting SetAsync after a fill means the position
        // cache misses the fill — safe (no-pyramiding OANDA fallback catches it) but
        // causes an unnecessary extra API call on the next signal.
        TradeResult result;
        using (TradeMetrics.OrderLatencySeconds.NewTimer())
        {
            result = await oanda.PlaceMarketOrderAsync(signal, stopLoss, takeProfit, units, CancellationToken.None);
        }

        if (result.Success)
        {
            TradeMetrics.OrdersPlaced.WithLabels("success").Inc();
            await positionCache.SetAsync(signal.Instrument, (decimal)(result.Units ?? units), CancellationToken.None);
            logger.LogInformation(
                "Order filled successfully for {Instrument}: ID={OrderId} @ {FillPrice} | Units={Units} | SL={SL} TP={TP}",
                signal.Instrument, result.OrderId, result.FillPrice, result.Units, result.StopLoss, result.TakeProfit);
        }
        else
        {
            TradeMetrics.OrdersPlaced.WithLabels("failed").Inc();
            logger.LogError("Order failed for {Instrument}: {Message} | Entry={Price} | SL={SL} | TP={TP}",
                signal.Instrument, result.Message, signal.Price.ToString(priceFmt), stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt));
        }
    }
}
