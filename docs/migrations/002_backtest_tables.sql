-- 002_backtest_tables.sql
-- Creates the four tables used by TradeFlowGuardian.Backtesting (EF Core / Npgsql).
-- Column names match EF Core's default PascalCase naming (no snake_case convention applied).
-- Run manually after 001_trade_history.sql.

-- ── Historical candle cache ───────────────────────────────────────────────────
-- Populated on first backtest run; subsequent runs over the same range skip OANDA fetches.

CREATE TABLE IF NOT EXISTS "HistoricalCandles" (
    "Id"         BIGSERIAL    PRIMARY KEY,
    "Instrument" VARCHAR(20)  NOT NULL,
    "Timeframe"  VARCHAR(10)  NOT NULL,
    "Timestamp"  TIMESTAMPTZ  NOT NULL,
    "Open"       NUMERIC(18,8) NOT NULL,
    "High"       NUMERIC(18,8) NOT NULL,
    "Low"        NUMERIC(18,8) NOT NULL,
    "Close"      NUMERIC(18,8) NOT NULL,
    "Volume"     BIGINT        NOT NULL DEFAULT 0,
    "Spread"     NUMERIC(10,6) NULL,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Instrument_Timeframe_Timestamp"
    ON "HistoricalCandles" ("Instrument", "Timeframe", "Timestamp");

CREATE INDEX IF NOT EXISTS "IX_Timestamp"
    ON "HistoricalCandles" ("Timestamp");

-- ── Backtest run metadata ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "BacktestRuns" (
    "Id"              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"            VARCHAR(200)  NOT NULL,
    "StrategyName"    VARCHAR(100)  NOT NULL,
    "StrategyConfig"  TEXT          NOT NULL,
    "Instrument"      VARCHAR(20)   NOT NULL,
    "Timeframe"       VARCHAR(10)   NOT NULL,
    "StartDate"       TIMESTAMPTZ   NOT NULL,
    "EndDate"         TIMESTAMPTZ   NOT NULL,
    "InitialBalance"  NUMERIC(18,2) NOT NULL,
    "FinalBalance"    NUMERIC(18,2) NOT NULL,
    "TotalReturn"     NUMERIC(10,6) NOT NULL DEFAULT 0,
    "MaxDrawdown"     NUMERIC(10,6) NOT NULL DEFAULT 0,
    "SharpeRatio"     NUMERIC(10,4) NULL,
    "SortinoRatio"    NUMERIC(10,4) NULL,
    "CalmarRatio"     NUMERIC(10,4) NULL,
    "ProfitFactor"    NUMERIC(10,4) NULL,
    "WinRate"         NUMERIC(10,6) NULL,
    "TotalTrades"     INT           NOT NULL DEFAULT 0,
    "WinningTrades"   INT           NOT NULL DEFAULT 0,
    "LosingTrades"    INT           NOT NULL DEFAULT 0,
    "AverageWin"      NUMERIC(18,6) NULL,
    "AverageLoss"     NUMERIC(18,6) NULL,
    "LargestWin"      NUMERIC(18,6) NULL,
    "LargestLoss"     NUMERIC(18,6) NULL,
    "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_BacktestRuns_CreatedAt"
    ON "BacktestRuns" ("CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_BacktestRuns_StrategyName_Instrument"
    ON "BacktestRuns" ("StrategyName", "Instrument");

-- ── Individual trades per run ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS "BacktestTrades" (
    "Id"            UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    "BacktestRunId" UUID          NOT NULL REFERENCES "BacktestRuns"("Id") ON DELETE CASCADE,
    "TradeNumber"   INT           NOT NULL,
    "Instrument"    VARCHAR(20)   NOT NULL,
    "Direction"     VARCHAR(10)   NOT NULL,
    "EntryTime"     TIMESTAMPTZ   NOT NULL,
    "EntryPrice"    NUMERIC(18,8) NOT NULL,
    "ExitTime"      TIMESTAMPTZ   NOT NULL,
    "ExitPrice"     NUMERIC(18,8) NOT NULL,
    "Units"         NUMERIC(18,2) NOT NULL,
    "StopLoss"      NUMERIC(18,8) NULL,
    "TakeProfit"    NUMERIC(18,8) NULL,
    "PnL"           NUMERIC(18,6) NOT NULL,
    "PnLPercent"    NUMERIC(10,6) NOT NULL,
    "Commission"    NUMERIC(18,6) NOT NULL DEFAULT 0,
    "Slippage"      NUMERIC(18,6) NOT NULL DEFAULT 0,
    "ExitReason"    VARCHAR(50)   NOT NULL DEFAULT '',
    "MAE"           NUMERIC(18,6) NULL,
    "MFE"           NUMERIC(18,6) NULL
);

CREATE INDEX IF NOT EXISTS "IX_BacktestTrades_RunId_TradeNumber"
    ON "BacktestTrades" ("BacktestRunId", "TradeNumber");

CREATE INDEX IF NOT EXISTS "IX_BacktestTrades_EntryTime"
    ON "BacktestTrades" ("EntryTime");

CREATE INDEX IF NOT EXISTS "IX_BacktestTrades_PnL"
    ON "BacktestTrades" ("PnL");

-- ── Equity curve (one row per bar while a position is open) ───────────────────

CREATE TABLE IF NOT EXISTS "BacktestEquityCurve" (
    "Id"              BIGSERIAL     PRIMARY KEY,
    "BacktestRunId"   UUID          NOT NULL REFERENCES "BacktestRuns"("Id") ON DELETE CASCADE,
    "Timestamp"       TIMESTAMPTZ   NOT NULL,
    "Balance"         NUMERIC(18,6) NOT NULL,
    "Equity"          NUMERIC(18,6) NOT NULL,
    "DrawdownPercent" NUMERIC(10,6) NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_BacktestEquityCurve_RunId_Timestamp"
    ON "BacktestEquityCurve" ("BacktestRunId", "Timestamp");
