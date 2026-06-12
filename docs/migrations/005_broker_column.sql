-- Migration: 005_broker_column
-- Created:   2026-06-12
-- Purpose:   Broker discriminator on the account registry, ahead of a second
--            broker adapter behind IBrokerClient. All existing rows are OANDA.
--            Not yet read or written by application code — the seam lands first.
--            Follow-up when broker #2 arrives: extend ux_oanda_accounts_account_env
--            to (broker, account_id, environment).

ALTER TABLE oanda_accounts
    ADD COLUMN IF NOT EXISTS broker VARCHAR(50) NOT NULL DEFAULT 'oanda';
