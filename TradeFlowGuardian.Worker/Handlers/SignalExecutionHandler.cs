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
            logger.LogWarning("Signal processing cancelled for {Instrument}", signal.Instrument);
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
                var set = await _db.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists);
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
            var closeResult = await oanda.ClosePositionAsync(signal.Instrument, ct);
            if (closeResult.Success)
            {
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

        // ── No pyramiding — check for existing position ───────────────────────
        var existingUnits = await oanda.GetOpenPositionUnitsAsync(signal.Instrument, ct);
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

        logger.LogInformation(
            "Executing {Direction} order for {Instrument} | Units={Units} | AccountBalance={Balance:C} | Entry={Price} | SL={SL} (dist: {StopDist}) | TP={TP} (dist: {TargetDist})",
            signal.Direction, signal.Instrument, units, balance, signal.Price, stopLoss, stopDistance, takeProfit, targetDistance);

        // ── Place order ───────────────────────────────────────────────────────
        TradeResult result;
        using (TradeMetrics.OrderLatencySeconds.NewTimer())
        {
            result = await oanda.PlaceMarketOrderAsync(signal, stopLoss, takeProfit, units, ct);
        }

        if (result.Success)
        {
            TradeMetrics.OrdersPlaced.WithLabels("success").Inc();
            logger.LogInformation(
                "Order filled successfully for {Instrument}: ID={OrderId} @ {FillPrice} | Units={Units} | SL={SL} TP={TP}",
                signal.Instrument, result.OrderId, result.FillPrice, result.Units, result.StopLoss, result.TakeProfit);
        }
        else
        {
            TradeMetrics.OrdersPlaced.WithLabels("failed").Inc();
            logger.LogError("Order failed for {Instrument}: {Message}", signal.Instrument, result.Message);
        }
    }
}
