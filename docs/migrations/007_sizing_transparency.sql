-- Migration: 007_sizing_transparency
-- Created:   2026-07-15
-- Purpose:   Persist the full position-sizing audit trail with every order attempt,
--            so the dashboard can show exactly how a trade size was reached
--            (risk %, account balance, ATR/stop distance, FX conversion, caps).
--            All columns nullable — Close signals and pre-migration rows carry NULLs.

ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS risk_percent    NUMERIC(6, 3);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS risk_source     TEXT;
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS account_balance NUMERIC(18, 2);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS risk_amount     NUMERIC(18, 2);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS atr             NUMERIC(18, 6);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS stop_distance   NUMERIC(18, 6);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS stop_source     TEXT;
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS quote_to_aud    NUMERIC(18, 8);
ALTER TABLE trade_history ADD COLUMN IF NOT EXISTS cap_reason      TEXT;

COMMENT ON COLUMN trade_history.risk_percent    IS 'Risk %% applied at sizing time';
COMMENT ON COLUMN trade_history.risk_source     IS 'signal-override | db | config-default';
COMMENT ON COLUMN trade_history.account_balance IS 'Account balance (AUD) at sizing time';
COMMENT ON COLUMN trade_history.risk_amount     IS 'AUD at risk if stop hit = balance x risk%%';
COMMENT ON COLUMN trade_history.atr             IS 'ATR from the signal (0 if signal supplied SL/TP)';
COMMENT ON COLUMN trade_history.stop_distance   IS 'Stop distance in quote-currency price units';
COMMENT ON COLUMN trade_history.stop_source     IS 'signal-sl | atrxN';
COMMENT ON COLUMN trade_history.quote_to_aud    IS 'Quote->AUD conversion rate used for sizing';
COMMENT ON COLUMN trade_history.cap_reason      IS 'NULL | margin-cap | max-position-units | aborted';
