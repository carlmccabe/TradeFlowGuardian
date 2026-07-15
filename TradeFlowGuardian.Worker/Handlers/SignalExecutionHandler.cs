using System.Text.Json;
using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Brokers;
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
    IBrokerClient broker,
    IPositionSizer sizer,
    IPositionCache positionCache,
    IDailyDrawdownGuard drawdownGuard,
    ITradeHistoryRepository tradeHistory,
    IRiskSettingsRepository riskRepo,
    IOptions<RiskConfig> risk,
    IConnectionMultiplexer redis,
    ILogger<SignalExecutionHandler> logger)
{
    private readonly RiskConfig _risk = risk.Value;
    private readonly IDatabase _db = redis.GetDatabase();
    private const string EventsChannel = "tradeflow:events";
    private static readonly TimeSpan DryRunResultTtl = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Stores the outcome of a dry-run signal under tradeflow:dryrun:{key} and publishes
    /// a dry_run_completed event. Stage names the pipeline step that decided the outcome.
    /// </summary>
    private async Task ReportDryRunAsync(
        TradeSignal signal, string stage, bool wouldTrade, string outcome, object? detail = null)
    {
        if (signal.IdempotencyKey is null)
        {
            logger.LogWarning("Dry run for {Instrument} has no idempotencyKey — result not retrievable", signal.Instrument);
            return;
        }
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                dryRun      = true,
                instrument  = signal.Instrument,
                direction   = signal.Direction.ToString(),
                stage,
                wouldTrade,
                outcome,
                detail,
                workerSha   = Services.BuildInfo.GitSha,
                completedAt = DateTimeOffset.UtcNow
            });
            await _db.StringSetAsync($"tradeflow:dryrun:{signal.IdempotencyKey}", payload, DryRunResultTtl);
            await PublishEventAsync(new
            {
                type = "dry_run_completed",
                instrument = signal.Instrument,
                key = signal.IdempotencyKey,
                stage,
                wouldTrade,
                outcome
            });
            logger.LogInformation("DRY RUN result for {Instrument}: stage={Stage} wouldTrade={WouldTrade} — {Outcome}",
                signal.Instrument, stage, wouldTrade, outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store dry-run result for {Key}", signal.IdempotencyKey);
        }
    }

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
        logger.LogInformation(
            "Processing signal: {Direction} {Instrument} | Price={Price} ATR={Atr} SL={SL} TP={TP} riskPercent={RiskPct} | Key={Key}",
            signal.Direction, signal.Instrument, signal.Price, signal.Atr,
            signal.StopLoss, signal.TakeProfit, signal.RiskPercent, signal.IdempotencyKey);

        if (signal.DryRun)
            logger.LogWarning("DRY RUN signal for {Instrument} — no order will be placed, no history written", signal.Instrument);

        // ── Idempotency check (skipped for dry runs so tests are repeatable) ──
        if (signal.IdempotencyKey is not null && !signal.DryRun)
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
                logger.LogInformation("Idempotency check passed: {Key}", signal.IdempotencyKey);
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
            if (signal.DryRun)
            {
                var openUnits = await broker.GetOpenPositionUnitsAsync(signal.Instrument, ct);
                await ReportDryRunAsync(signal, "close", openUnits is not null,
                    openUnits is not null
                        ? $"Would close open position of {openUnits} units"
                        : "No open position to close",
                    new { openUnits });
                return;
            }

            // CancellationToken.None — must not abort a close request once sent.
            var closeResult = await broker.ClosePositionAsync(signal.Instrument, CancellationToken.None);
            await tradeHistory.InsertAsync(new TradeHistoryRecord
            {
                Instrument   = signal.Instrument,
                Direction    = signal.Direction.ToString(),
                EntryPrice   = 0,
                Units        = 0,
                FillPrice    = closeResult.FillPrice,
                OrderId      = closeResult.OrderId,
                Success      = closeResult.Success,
                ErrorMessage = closeResult.Success ? null : closeResult.Message,
                ExecutedAt   = closeResult.ExecutedAt
            }, CancellationToken.None);

            if (closeResult.Success)
            {
                await positionCache.ClearAsync(signal.Instrument, CancellationToken.None);
                logger.LogInformation("Successfully closed position for {Instrument}: {Message}",
                    signal.Instrument, closeResult.Message);
                await PublishEventAsync(new
                {
                    type       = "position_closed",
                    instrument = signal.Instrument,
                    exitPrice  = closeResult.FillPrice,
                    orderId    = closeResult.OrderId,
                    status     = "closed"
                });
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
            logger.LogWarning("Signal for {Instrument} BLOCKED by filter [{Label}]: {Reason}",
                signal.Instrument, filterResult.Label, filterResult.Reason);
            if (signal.DryRun)
            {
                await ReportDryRunAsync(signal, "filters", false,
                    $"Blocked by filter [{filterResult.Label}]: {filterResult.Reason}");
                return;
            }
            TradeMetrics.SignalsFiltered.WithLabels(filterResult.Label).Inc();
            return;
        }
        logger.LogInformation("Filters passed for {Instrument}", signal.Instrument);

        // ── IsActive check — DB risk settings ────────────────────────────────
        var riskSettings = await riskRepo.GetByInstrumentAsync(signal.Instrument, ct);
        if (riskSettings is null)
        {
            logger.LogWarning(
                "Risk settings unavailable for {Instrument} (DB returned null — Postgres may not be connected). " +
                "IsActive check skipped; proceeding with signal.",
                signal.Instrument);
        }
        else if (!riskSettings.IsActive)
        {
            logger.LogWarning("Signal for {Instrument} BLOCKED: instrument is inactive in risk settings", signal.Instrument);
            if (signal.DryRun)
            {
                await ReportDryRunAsync(signal, "risk-settings", false, "Instrument is inactive in risk settings");
                return;
            }
            TradeMetrics.SignalsFiltered.WithLabels("instrument_inactive").Inc();
            return;
        }
        else
        {
            logger.LogInformation("Risk settings OK for {Instrument}: isActive=true riskPct={RiskPct}%",
                signal.Instrument, riskSettings.RiskPercent);
        }

        // ── No pyramiding — check for existing position (cache → OANDA fallback) ──
        var (cached, cachedUnits) = await positionCache.GetAsync(signal.Instrument, ct);
        decimal? existingUnits;
        if (cached)
        {
            existingUnits = cachedUnits;
            logger.LogInformation("Position cache hit for {Instrument}: {Units} units", signal.Instrument, cachedUnits);
        }
        else
        {
            existingUnits = await broker.GetOpenPositionUnitsAsync(signal.Instrument, ct);
            if (existingUnits is not null)
                await positionCache.SetAsync(signal.Instrument, existingUnits.Value, ct);
        }

        if (existingUnits is not null)
        {
            logger.LogWarning(
                "Signal SKIPPED: Position already open on {Instrument} ({Units} units). No pyramiding.",
                signal.Instrument, existingUnits);
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "no-pyramiding", false,
                    $"Position already open ({existingUnits} units) — entry would be skipped");
            return;
        }
        logger.LogInformation("No open position on {Instrument} — proceeding to size order", signal.Instrument);

        // ── Position sizing ───────────────────────────────────────────────────
        var balance = await broker.GetAccountBalanceAsync(ct);
        if (balance <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Could not fetch account balance or balance is non-positive ({Balance:C})",
                signal.Instrument, balance);
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "balance", false,
                    $"Could not fetch account balance (got {balance}) — check broker credentials");
            return;
        }
        logger.LogInformation("Account balance: {Balance:C} AUD", balance);

        TradeMetrics.AccountBalance.Set((double)balance);

        // ── Daily drawdown circuit breaker ────────────────────────────────────────
        // EnsureDayOpenNav is a SetNX — only fires once per UTC day.
        // Dry runs read the breach state without marking it (no side effects).
        await drawdownGuard.EnsureDayOpenNavAsync(balance, ct);
        var breached = signal.DryRun
            ? await drawdownGuard.IsBreachedAsync(ct)
            : await drawdownGuard.CheckAndMarkIfBreachedAsync(balance, ct);
        if (breached)
        {
            logger.LogWarning(
                "Aborting signal for {Instrument}: daily drawdown limit breached (confirmed at balance fetch).",
                signal.Instrument);
            if (signal.DryRun)
            {
                await ReportDryRunAsync(signal, "drawdown", false, "Daily drawdown circuit breaker is tripped");
                return;
            }
            TradeMetrics.SignalsFiltered.WithLabels("daily_drawdown").Inc();
            await PublishEventAsync(new { type = "drawdown_breached", balance });
            return;
        }

        var sizing = await sizer.CalculateUnitsAsync(signal, balance, ct);
        var units  = sizing.Units;
        if (units <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Position size calculated as {Units} units. Check ATR ({Atr}) and risk configuration (Risk%: {RiskPercent}%, Balance: {Balance:C})",
                signal.Instrument, units, signal.Atr, signal.RiskPercent > 0 ? signal.RiskPercent : _risk.DefaultRiskPercent, balance);
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "sizing", false, "Position size calculated as 0 units", sizing);
            return;
        }

        // ── SL / TP ───────────────────────────────────────────────────────────
        var isJpy    = signal.Instrument.Contains("JPY");
        var priceFmt = isJpy ? "F3" : "F5";

        // TODO (robustness): fetch current OANDA bid/ask and verify SL/TP straddle the
        // live price before submitting. This would catch stale pre-calculated levels and
        // prevent TAKE_PROFIT_ON_FILL_LOSS rejections. Requires one extra /pricing call —
        // consider sharing the price already fetched by PositionSizer to avoid a second round-trip.
        decimal stopLoss, takeProfit;
        if (signal.StopLoss > 0 && signal.TakeProfit > 0)
        {
            // Pre-calculated SL/TP from Pine Script — trusted as-is.
            // Price is optional in this path; a signal with price=0 is valid.
            stopLoss   = signal.StopLoss;
            takeProfit = signal.TakeProfit;
            logger.LogInformation("SL/TP from signal: {Instrument} SL={SL} TP={TP}",
                signal.Instrument, stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt));
        }
        else
        {
            // ATR-based server-side calculation — requires both Price and ATR.
            if (signal.Price <= 0 || signal.Atr <= 0)
            {
                logger.LogError(
                    "Aborting signal for {Instrument}: ATR-based SL/TP requires Price and ATR " +
                    "(Price={Price}, Atr={Atr}). Send pre-calculated StopLoss/TakeProfit or include both.",
                    signal.Instrument, signal.Price, signal.Atr);
                if (signal.DryRun)
                    await ReportDryRunAsync(signal, "sl-tp", false,
                        $"ATR-based SL/TP requires Price and ATR (Price={signal.Price}, Atr={signal.Atr})");
                return;
            }

            logger.LogInformation("SL/TP calculated from ATR: {Instrument} Price={Price} ATR={Atr} stop×{StopMult} target×{TargetMult}",
                signal.Instrument, signal.Price.ToString(priceFmt), signal.Atr,
                _risk.AtrStopMultiplier, _risk.AtrTargetMultiplier);

            var stopDistance   = signal.Atr * _risk.AtrStopMultiplier;
            var targetDistance = signal.Atr * _risk.AtrTargetMultiplier;

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
        }

        // ── SL/TP Sanity Check ────────────────────────────────────────────────
        if (stopLoss <= 0 || takeProfit <= 0)
        {
            logger.LogError("Aborting signal for {Instrument}: Invalid SL/TP (SL={SL}, TP={TP}). Check ATR ({Atr}) and signal values.",
                signal.Instrument, stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt), signal.Atr);
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "sl-tp", false,
                    $"Invalid SL/TP (SL={stopLoss.ToString(priceFmt)}, TP={takeProfit.ToString(priceFmt)})");
            return;
        }

        if (signal.Direction == SignalDirection.Long && takeProfit <= stopLoss)
        {
            logger.LogError("Aborting signal for {Instrument}: TP ({TP}) is not above SL ({SL}) for LONG",
                signal.Instrument, takeProfit.ToString(priceFmt), stopLoss.ToString(priceFmt));
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "sl-tp", false,
                    $"TP ({takeProfit.ToString(priceFmt)}) is not above SL ({stopLoss.ToString(priceFmt)}) for LONG");
            return;
        }

        if (signal.Direction == SignalDirection.Short && takeProfit >= stopLoss)
        {
            logger.LogError("Aborting signal for {Instrument}: TP ({TP}) is not below SL ({SL}) for SHORT",
                signal.Instrument, takeProfit.ToString(priceFmt), stopLoss.ToString(priceFmt));
            if (signal.DryRun)
                await ReportDryRunAsync(signal, "sl-tp", false,
                    $"TP ({takeProfit.ToString(priceFmt)}) is not below SL ({stopLoss.ToString(priceFmt)}) for SHORT");
            return;
        }

        logger.LogInformation(
            "Executing {Direction} order for {Instrument} | Units={Units} | AccountBalance={Balance:C} | SL={SL} | TP={TP}",
            signal.Direction, signal.Instrument, units, balance, stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt));

        // ── Dry run stops here: everything checked, nothing submitted ─────────
        if (signal.DryRun)
        {
            var entryRef  = signal.Price > 0 ? signal.Price : (decimal?)null;
            var projLoss  = Math.Round(sizing.LossPerUnit * units, 2);
            var projProfit = entryRef is not null
                ? Math.Round(Math.Abs(takeProfit - entryRef.Value) * units * sizing.QuoteToAud, 2)
                : (decimal?)null;
            await ReportDryRunAsync(signal, "ready-to-order", true,
                $"Would place {signal.Direction} {units} units, SL {stopLoss.ToString(priceFmt)} / TP {takeProfit.ToString(priceFmt)}",
                new
                {
                    units,
                    stopLoss,
                    takeProfit,
                    projectedLossAud   = projLoss,
                    projectedProfitAud = projProfit,
                    balance,
                    sizing
                });
            return;
        }

        // ── Place order ───────────────────────────────────────────────────────
        // CancellationToken.None for both calls: once we decide to trade, we must
        // see it through. Aborting PlaceMarketOrderAsync mid-flight leaves the order
        // state at OANDA unknown. Aborting SetAsync after a fill means the position
        // cache misses the fill — safe (no-pyramiding OANDA fallback catches it) but
        // causes an unnecessary extra API call on the next signal.
        TradeResult result;
        using (TradeMetrics.OrderLatencySeconds.NewTimer())
        {
            result = await broker.PlaceMarketOrderAsync(signal, stopLoss, takeProfit, units, CancellationToken.None);
        }

        await tradeHistory.InsertAsync(new TradeHistoryRecord
        {
            Instrument   = signal.Instrument,
            Direction    = signal.Direction.ToString(),
            EntryPrice   = signal.Price,
            StopLoss     = stopLoss,
            TakeProfit   = takeProfit,
            Units        = units,
            FillPrice    = result.FillPrice,
            OrderId      = result.OrderId,
            Success      = result.Success,
            ErrorMessage = result.Success ? null : result.Message,
            ExecutedAt   = result.ExecutedAt,
            // Sizing audit trail — how these units were reached (migration 007)
            RiskPercent    = sizing.RiskPercent,
            RiskSource     = sizing.RiskSource,
            AccountBalance = sizing.AccountBalance,
            RiskAmount     = sizing.RiskAmount,
            Atr            = sizing.Atr,
            StopDistance   = sizing.StopDistance,
            StopSource     = sizing.StopSource,
            QuoteToAud     = sizing.QuoteToAud,
            CapReason      = sizing.CapReason
        }, CancellationToken.None);

        if (result.Success)
        {
            TradeMetrics.OrdersPlaced.WithLabels("success").Inc();
            await positionCache.SetAsync(signal.Instrument, (decimal)(result.Units ?? units), CancellationToken.None);
            logger.LogInformation(
                "Order filled successfully for {Instrument}: ID={OrderId} @ {FillPrice} | Units={Units} | SL={SL} TP={TP}",
                signal.Instrument, result.OrderId, result.FillPrice, result.Units, result.StopLoss, result.TakeProfit);
            await PublishEventAsync(new
            {
                type         = "order_filled",
                instrument   = signal.Instrument,
                direction    = signal.Direction.ToString(),
                units        = result.Units ?? units,
                entryPrice   = result.FillPrice,
                stopLoss,
                takeProfit,
                orderId      = result.OrderId,
                unrealisedPnl = 0m,
                status       = "open"
            });
        }
        else
        {
            TradeMetrics.OrdersPlaced.WithLabels("failed").Inc();
            logger.LogError("Order failed for {Instrument}: {Message} | Entry={Price} | SL={SL} | TP={TP}",
                signal.Instrument, result.Message, signal.Price.ToString(priceFmt), stopLoss.ToString(priceFmt), takeProfit.ToString(priceFmt));
        }
    }

    private async Task PublishEventAsync(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(EventsChannel), json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish trade event to Redis");
        }
    }
}
