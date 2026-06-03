-- Migration: 003_risk_settings
-- Created:   2026-06-03
-- Purpose:   Per-instrument risk settings — risk %, active flag.
--            Managed via EF Core (TradeFlowDbContext) with migration 20260603000000_InitRiskSettings.
--            Run this file manually OR apply via: dotnet ef database update --project TradeFlowGuardian.Infrastructure

CREATE TABLE IF NOT EXISTS risk_settings (
    instrument   VARCHAR(20)    PRIMARY KEY,
    risk_percent NUMERIC(5,4)   NOT NULL DEFAULT 1.5,
    is_active    BOOLEAN        NOT NULL DEFAULT true,
    updated_at   TIMESTAMPTZ    NOT NULL DEFAULT now()
);

INSERT INTO risk_settings (instrument, risk_percent, is_active, updated_at)
VALUES
    ('USD_JPY', 1.5, true, '2026-06-03T00:00:00Z'),
    ('EUR_USD', 1.5, true, '2026-06-03T00:00:00Z'),
    ('GBP_USD', 1.5, true, '2026-06-03T00:00:00Z')
ON CONFLICT (instrument) DO NOTHING;
