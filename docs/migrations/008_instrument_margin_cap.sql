-- Migration: 008_instrument_margin_cap
-- Created:   2026-07-22
-- Purpose:   Per-instrument margin utilisation cap. NULL means "use the config
--            default" (Risk:DefaultMarginCapPercent, 28%). USD_JPY is raised to
--            50% so it can size at its full 2.5% risk in low-ATR conditions
--            where the flat 28% cap was clipping entries. Existing risk_percent
--            rows are not touched.

ALTER TABLE risk_settings ADD COLUMN IF NOT EXISTS margin_cap_percent NUMERIC(5, 2);

COMMENT ON COLUMN risk_settings.margin_cap_percent IS
    'Max %% of account margin a single trade on this instrument may consume. NULL = config default (28).';

UPDATE risk_settings
SET    margin_cap_percent = 50,
       updated_at         = NOW()
WHERE  instrument = 'USD_JPY';

-- cap_reason gains a new value now that the aggregate margin safety net exists
COMMENT ON COLUMN trade_history.cap_reason IS
    'NULL | margin-cap | aggregate-margin-cap | max-position-units | aborted';
