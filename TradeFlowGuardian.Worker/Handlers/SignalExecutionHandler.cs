using TradeFlowGuardian.Core.Configuration;
using TradeFlowGuardian.Core.Enums;
using TradeFlowGuardian.Core.Interfaces;
using TradeFlowGuardian.Core.Models;
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
public class SignalExecutionHandler
{
    private readonly ISignalFilter _filter;
    private readonly IOandaClient _oanda;
    private readonly IPositionSizer _sizer;
    private readonly RiskConfig _risk;
    private readonly ILogger<SignalExecutionHandler> _logger;

    // Simple in-memory idempotency set — Phase 2: move to Redis with TTL
    private static readonly HashSet<string> ProcessedKeys = new();

    public SignalExecutionHandler(
        ISignalFilter filter,
        IOandaClient oanda,
        IPositionSizer sizer,
        IOptions<RiskConfig> risk,
        ILogger<SignalExecutionHandler> logger)
    {
        _filter = filter;
        _oanda = oanda;
        _sizer = sizer;
        _risk = risk.Value;
        _logger = logger;
    }

    public async Task HandleAsync(TradeSignal signal, CancellationToken ct)
    {
        using var scope = _logger.BeginScope(
            "Signal {Direction} {Instrument} {Key}",
            signal.Direction, signal.Instrument, signal.IdempotencyKey);

        // ── Idempotency check ─────────────────────────────────────────────────
        if (signal.IdempotencyKey is not null)
        {
            if (ProcessedKeys.Contains(signal.IdempotencyKey))
            {
                _logger.LogWarning("Duplicate signal ignored: {Key}", signal.IdempotencyKey);
                return;
            }
            ProcessedKeys.Add(signal.IdempotencyKey);
            // Prevent unbounded growth — keep last 500 keys
            if (ProcessedKeys.Count > 500)
                ProcessedKeys.Clear();
        }

        // ── Handle Close signal ───────────────────────────────────────────────
        if (signal.Direction == SignalDirection.Close)
        {
            var closeResult = await _oanda.ClosePositionAsync(signal.Instrument, ct);
            _logger.LogInformation("Close result: {Success} — {Message}",
                closeResult.Success, closeResult.Message);
            return;
        }

        // ── Run signal filters ────────────────────────────────────────────────
        var filterResult = await _filter.EvaluateAsync(signal, ct);
        if (!filterResult.Allowed)
        {
            _logger.LogWarning("Signal blocked by filter: {Reason}", filterResult.Reason);
            return;
        }

        // ── No pyramiding — check for existing position ───────────────────────
        var existingUnits = await _oanda.GetOpenPositionUnitsAsync(signal.Instrument, ct);
        if (existingUnits is not null)
        {
            _logger.LogWarning(
                "Position already open on {Instrument} ({Units} units) — signal skipped",
                signal.Instrument, existingUnits);
            return;
        }

        // ── Position sizing ───────────────────────────────────────────────────
        var balance = await _oanda.GetAccountBalanceAsync(ct);
        if (balance <= 0)
        {
            _logger.LogError("Could not fetch account balance — aborting");
            return;
        }

        var units = await _sizer.CalculateUnitsAsync(signal, balance, ct);
        if (units <= 0)
        {
            _logger.LogError("Position size calculated as 0 — check ATR and risk config");
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

        _logger.LogInformation(
            "Executing {Direction} | Units={Units} | Balance={Balance:C} | SL={SL} TP={TP}",
            signal.Direction, units, balance, stopLoss, takeProfit);

        // ── Place order ───────────────────────────────────────────────────────
        var result = await _oanda.PlaceMarketOrderAsync(signal, stopLoss, takeProfit, units, ct);

        if (result.Success)
            _logger.LogInformation(
                "Order filled: ID={OrderId} @ {FillPrice} | Units={Units} | SL={SL} TP={TP}",
                result.OrderId, result.FillPrice, result.Units, result.StopLoss, result.TakeProfit);
        else
            _logger.LogError("Order failed: {Message}", result.Message);
    }
}
