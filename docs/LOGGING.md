# Centralized Logging

> **Quick start on Railway:** See [docs/SEQ.md](./SEQ.md) to run a self-hosted SEQ instance —
> no Grafana account required, just change two env vars.

## Grafana Cloud

Both services ship structured logs to Grafana Cloud (Loki) via OpenTelemetry OTLP,
**in addition to** the JSON console logs that Railway's own log viewer reads. The
exporter activates only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set — without it the
app behaves exactly as before (local dev, tests, CI are all unaffected).

Implementation: `AddOtlpExportIfConfigured()` in
[`TradeFlowGuardian.Infrastructure/Logging/OtlpLoggingExtensions.cs`](../TradeFlowGuardian.Infrastructure/Logging/OtlpLoggingExtensions.cs),
called from both `Program.cs` files. Service names: `tradeflow-api`, `tradeflow-worker`.
Export is batched in the background — a Grafana outage never blocks or crashes the app.

## One-time Grafana Cloud setup

1. In the [Grafana Cloud portal](https://grafana.com/profile/org), open your stack and
   find the **OpenTelemetry** card → **Configure**. It shows:
   - the OTLP endpoint, e.g. `https://otlp-gateway-prod-au-southeast-1.grafana.net/otlp`
   - your **instance ID** (a number)
   - a button to generate an **API token** (scope: `metrics:write logs:write traces:write`,
     or just write-all for the stack)
2. Build the Basic-auth header value locally (never commit it):
   ```sh
   echo -n "<instanceId>:<token>" | base64
   ```

## Railway env vars (set on BOTH Api and Worker services, both environments)

```
OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp-gateway-<your-zone>.grafana.net/otlp
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64 from step 2>
```

`http/protobuf` is required — Grafana Cloud's OTLP gateway does not accept gRPC.
No code or config changes are needed per environment; staging vs production is
distinguished automatically via the `deployment_environment` label (taken from
Railway's `RAILWAY_ENVIRONMENT_NAME`).

## Querying

In Grafana: **Drilldown → Logs**, or **Explore** with the `grafanacloud-*-logs` (Loki)
data source:

```logql
{service_name="tradeflow-worker"}                          # one service
{service_name=~"tradeflow-.+"} |= "USDJPY_1744329600"      # a signal across both services
{service_name=~"tradeflow-.+"} | LogLevel = "Error"        # errors everywhere
```

Structured log properties (instrument, idempotency key, etc.) arrive as OTLP log
attributes and are queryable with `| attribute = "value"` filters.

## Local verification

Point the exporter at any HTTP listener and watch for protobuf POSTs to `/v1/logs`:

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4319 \
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf \
dotnet TradeFlowGuardian.Api.dll
```

The full local Grafana/Loki/Prometheus stack in `docker-compose.yml`
(`--profile monitoring`) remains available for offline dev and is unrelated to
the cloud setup.
