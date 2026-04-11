-- Migration: 001_trade_history
-- Created:   2026-04-11
-- Purpose:   Persists every order attempt (entry or close) for audit and P&L reporting.

CREATE TABLE IF NOT EXISTS trade_history (
    id            BIGSERIAL       PRIMARY KEY,
    instrument    TEXT            NOT NULL,
    direction     TEXT            NOT NULL,           -- 'Long', 'Short', 'Close'
    entry_price   NUMERIC(18, 5)  NOT NULL,           -- signal bar price; 0 for Close signals
    sl            NUMERIC(18, 5),                     -- computed stop-loss; NULL for Close signals
    tp            NUMERIC(18, 5),                     -- computed take-profit; NULL for Close signals
    units         BIGINT          NOT NULL,           -- requested units; 0 for Close signals
    fill_price    NUMERIC(18, 5),                     -- actual OANDA fill price; NULL on failure
    order_id      TEXT,                               -- OANDA order/trade ID; NULL on failure
    success       BOOLEAN         NOT NULL,
    error_message TEXT,                               -- OANDA error or internal reason; NULL on success
    executed_at   TIMESTAMPTZ     NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_trade_history_instrument   ON trade_history (instrument);
CREATE INDEX IF NOT EXISTS idx_trade_history_executed_at  ON trade_history (executed_at DESC);
