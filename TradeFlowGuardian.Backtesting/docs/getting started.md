# TradeFlowGuardian Codebase Orientation Guide

Audience: New contributors with little or no trading background  
Goal: Help you understand what this system does, how it’s structured, and how to run, extend, and validate it safely.

---

## 1) What this system is

- A Forex strategy research platform that:
    - Imports historical market data
    - Runs backtests of trading strategies
    - Analyzes performance
    - Can operate a live bot via OANDA (a broker API)

Think of it as three layers:
- Data: get, store, and validate market candles
- Strategy: decide when to buy/sell, with filters and risk rules
- Analysis/Execution: evaluate performance or place live trades

---

## 2) Key concepts (trading made simple)

- Candle: a time-bucketed price summary (Open/High/Low/Close, plus volume)
- Strategy: logic that outputs Buy/Sell/Hold or Exit
- Risk: how big a position to take and where to place stop-loss/take-profit
- Backtest: run the strategy on past data to see hypothetical results
- Metrics: win rate, drawdown, Sharpe ratio, profit factor, etc.

You don’t need to be a trader to work here—just remember:
- Buy/Sell is an entry
- Exit closes an open position
- Stop-loss limits loss; take-profit locks in profit
- Risk per trade controls position size

---

## 3) Solution structure and responsibilities

- Backtesting
    - Loads historical candles from DB or OANDA if missing/gapped
    - Runs strategies candle-by-candle
    - Tracks equity, trades, drawdowns, and metrics
    - Saves results for reporting

- Strategies
    - Built from composable parts:
        - Signals: generate Buy/Sell candidates
        - Filters: reject weak or risky setups
        - Risk calculators: compute stop-loss/take-profit
        - Optional exit strategies: tell when to close early

- Console app (CLI)
    - import-data: fetch and store candles
    - test-strategy: run a single strategy
    - compare-strategies: rank several strategies
    - list-results: show past backtests

- Infrastructure (OANDA)
    - Wraps OANDA REST API for prices, candles, orders, and positions
    - Includes safety checks (e.g., spread filtering before placing a live order)

- Live bot
    - Periodically evaluates one strategy with recent candles
    - Checks risk, account limits, and throttles trade attempts
    - Places market orders with confirmations and status reporting

---

## 4) Typical developer workflows

- Run a backtest
    1) Import data: import-data EUR_USD 2023-01-01 2024-12-31
    2) Test a strategy: test-strategy emac EUR_USD 12m
    3) Review metrics in the console output
    4) list-results to view recent runs

- Compare strategies
    - compare-strategies EUR_USD 24m
    - See a ranked table and a deployment recommendation

- Add a new strategy
    1) Implement one or more ISignalGenerator, IFilter, and IRiskCalculator
    2) Combine them with StrategyBuilder in a preset method
    3) Use the CLI to backtest and compare

- Enhance data reliability
    - Use the historical provider; it detects gaps and fills from OANDA in chunks
    - Ensure your DB connection is configured and reachable

---

## 5) Strategy composition model

- StrategyBuilder assembles:
    - Signals: produce potential entries with a confidence and reason
    - Filters: veto risky entries (e.g., low volatility, bad time window)
    - RiskCalculator: decides stop-loss/take-profit
    - ExitStrategy (optional): triggers early exits

Evaluation flow:
- If there’s an open position and an exit rule triggers -> Exit
- If already in a trade -> Hold
- Otherwise gather all signals, pick the strongest, run through filters
- If allowed, compute risk params and return Buy/Sell with SL/TP

This design lets you swap components without rewriting the engine.

---

## 6) Backtesting engine behavior (simplified)

- Loads candles for the timeframe and period
- Warms up with initial data to ensure indicators are ready
- Loops through each candle:
    - Updates equity and drawdown
    - Closes an open trade if SL/TP is hit
    - If flat, calls strategy.Evaluate to decide on a new entry
    - Sizes position by risk per trade and stop distance
- Computes metrics: return, Sharpe, drawdown, profit factor, etc.
- Saves results: summary, trades, sampled equity curve

Notes:
- It uses a simple pip-size heuristic (handle JPY pairs differently)
- It models commissions and a small slippage approximation
- It serializes strategy identity for auditability

---

## 7) Data pipeline overview

- First try DB for requested candles
- If coverage is insufficient or gaps are detected:
    - Fetch the missing window(s) from OANDA in bounded chunks
    - Store to DB and return merged, ordered candles
- Basic weekend and granularity-aware checks to detect gaps

Why it matters:
- Deterministic, complete data → stable backtests
- Avoids bias from missing candles

---

## 8) Console commands

- import-data <instrument> <start> <end>
    - Pulls and stores M5 candles by monthly chunks, de-duplicating

- test-strategy <strategy-name> <instrument> <period>
    - Runs a single strategy and shows a performance report

- compare-strategies <instrument> <period>
    - Runs multiple presets, prints a ranked table, and gives a deployment suggestion

- list-results
    - Prints last 20 runs from the database

Environment
- Requires OANDA API credentials configured in appsettings or env vars for commands that hit the API

---

## 9) Live bot (high level)

- Designed for one strategy and instrument
- Every few seconds:
    - Refresh account, price, and position state
    - Enforce daily loss and per-day trade limits
    - Read recent M5 candles
    - Evaluate strategy and possibly enter/exit
    - Place orders with confirmation and spread guard-rails
- Prevents overlapping trades and over-firing via locks and timers
- Provides a concise status (current price, position, PnL, actions)

---

## 10) Performance analysis basics

Common metrics shown:
- Return and final balance
- Max drawdown (worst equity drop)
- Win rate and profit factor (gross gains / gross losses)
- Sharpe and Sortino (risk-adjusted returns)
- Calmar (annualized return vs max drawdown)
- Average win/loss and largest win/loss
- Monthly breakdown to spot consistency

Rule of thumb:
- High Sharpe, acceptable drawdown, profit factor > 1.2, and consistent monthly returns are good signs.
- Beware high returns with poor drawdown/consistency.

---

## 11) Quality and reliability practices

- Determinism: given the same data and parameters, results should match
- Data hygiene: ensure coverage and gap-filling
- Edge cases: empty signals, zero stop distance, wide spreads
- Logging: backtests and bot actions are verbose to aid debugging
- Persistence: backtest runs, trades, and equity curves are saved to DB

---

## 12) How to extend safely

- New signal/filter/risk components:
    - Keep inputs immutable (read-only candle lists and context)
    - Return clear reasons for decisions to aid debugging
    - Validate parameters (e.g., periods > 0)

- New performance metrics:
    - Add computations after the backtest loop using trades/equity
    - Display in ConsoleReporter for visibility

- Data sources or timeframes:
    - Ensure chunking limits and completeness checks
    - Normalize candle times to UTC and align to timeframe boundaries

- Live trading:
    - Keep conservative defaults (risk, spread guards, retry limits)
    - Fail closed: if uncertain, skip or exit rather than over-trade

---

## 13) Running locally

- Prerequisites
    - .NET 9 SDK
    - SQL Server (or update connection string to your DB)
    - OANDA Practice API key and account ID for API-backed tasks

- Quick start
    - Configure OANDA settings in appsettings or environment variables
    - Run the console and use the commands listed above
    - Inspect logs and the database for saved runs

---

## 14) Roadmap highlights (what’s planned)

- Stronger data validation and market context
- Strategy validation framework (determinism, parameter sensitivity)
- Walk-forward optimization and richer analytics
- Broader test coverage and performance tuning
- More realistic execution (spread/slippage modeling)

---

## 15) Glossary (plain English)

- ATR: Average True Range, a volatility measure often used for stop sizing
- Drawdown: The peak-to-trough drop in equity; lower is safer
- Sharpe ratio: Return relative to volatility; higher is better
- Profit factor: Gross profit / gross loss; >1 means profitable
- Spread: Difference between bid and ask; wider spreads increase costs
- Slippage: Executed price deviates from requested; can reduce profit

---

If you’re new, start by:
1) Running import-data and test-strategy to see a full loop end-to-end
2) Opening Strategy presets to grasp how signals/filters/risk combine
3) Reading the console’s performance output and monthly breakdown
4) Trying compare-strategies to see ranking and recommendations

Questions welcome—your “AI Assistant” is here to help.