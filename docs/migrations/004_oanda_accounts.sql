-- Migration: 004_oanda_accounts
-- Created:   2026-06-11
-- Purpose:   OANDA account registry — shared by Api and Worker so both always
--            trade/read the same account. Replaces Oanda__AccountId/ApiKey env vars.
--            api_key_encrypted is protected with ASP.NET Data Protection
--            (keys persisted to Redis, application name "TradeFlowGuardian").
--            Run this file manually against Postgres.

CREATE TABLE IF NOT EXISTS oanda_accounts (
    id                 UUID          PRIMARY KEY,
    label              VARCHAR(100)  NOT NULL,
    account_id         VARCHAR(50)   NOT NULL,
    environment        VARCHAR(20)   NOT NULL DEFAULT 'fxpractice',  -- fxpractice | fxtrade
    api_key_encrypted  TEXT          NOT NULL,
    is_active          BOOLEAN       NOT NULL DEFAULT false,
    created_at         TIMESTAMPTZ   NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT chk_oanda_environment CHECK (environment IN ('fxpractice', 'fxtrade'))
);

-- At most one account may be active at any time.
CREATE UNIQUE INDEX IF NOT EXISTS ux_oanda_accounts_one_active
    ON oanda_accounts (is_active)
    WHERE is_active;

CREATE UNIQUE INDEX IF NOT EXISTS ux_oanda_accounts_account_env
    ON oanda_accounts (account_id, environment);
