-- Migration: 006_risk_percent_2_5
-- Created:   2026-06-16
-- Purpose:   Correct seeded risk_percent from 1.5 to 2.5 for all three pairs.
--            003_risk_settings seeded 1.5% as a placeholder; live risk parameters
--            per CLAUDE.md are 2.5% across USD_JPY, EUR_USD, and GBP_USD.
--            ON CONFLICT DO NOTHING means rows added after initial seed are untouched.

UPDATE risk_settings
SET    risk_percent = 2.5,
       updated_at   = NOW()
WHERE  instrument IN ('USD_JPY', 'EUR_USD', 'GBP_USD')
  AND  risk_percent = 1.5;
