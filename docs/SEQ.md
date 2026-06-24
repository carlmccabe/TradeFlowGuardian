# SEQ — Structured Log Server

SEQ is a self-hosted log server with a web UI for searching and filtering structured logs.
It is simpler to operate than the Grafana/Loki/Promtail stack and is the recommended log viewer
for the Railway deployment.

The .NET apps ship logs via the same OpenTelemetry OTLP exporter already used for Grafana Cloud —
only the env vars change. No code modifications required.

---

## Local dev

SEQ is included in `docker-compose.yml` under the `seq` and `monitoring` profiles.

```sh
# SEQ only (lightweight — no Prometheus/Grafana):
docker compose --profile core --profile seq up --build

# Full stack (Prometheus + Grafana + SEQ):
docker compose --profile core --profile monitoring up --build
```

Then add OTLP env vars to the `api` and `worker` services in `docker-compose.yml`
(or export them in your shell before `docker compose up`):

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://seq:5341/ingest/otlp
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

Web UI: **http://localhost:8081** (no password required in dev).

---

## Railway deployment

Railway does not have a built-in SEQ template, so you add it as a Docker image service.

### 1 — Add the SEQ service

1. Open the Railway project → **+ New** → **Docker Image**
2. Image: `datalust/seq:latest`
3. Under **Settings → Service name**, call it `seq` (the name determines the private hostname)

### 2 — Configure environment variables

Set these on the SEQ service:

| Variable | Value |
|---|---|
| `ACCEPT_EULA` | `Y` |
| `SEQ_FIRSTRUN_ADMINPASSWORDHASH` | *(see below)* |

**Generating the password hash** (run locally, never commit the result):

```sh
docker run --rm datalust/seq config hash --plaintext 'your-strong-password'
```

Paste the output as the `SEQ_FIRSTRUN_ADMINPASSWORDHASH` value in Railway.

### 3 — Enable the public domain

In the SEQ service → **Settings → Networking → Public Networking**, click **Generate Domain**.
This is the URL you open in a browser to view logs.
Port to expose: **80** (SEQ web UI).

### 4 — Enable private networking

In the Railway project settings, enable **Private Networking**. This gives each service a stable
internal hostname: `<service-name>.railway.internal`.

With the service named `seq`, the OTLP endpoint reachable from Api and Worker is:

```
http://seq.railway.internal:5341/ingest/otlp
```

### 5 — Point Api and Worker at SEQ

Add these variables to **both** the Api and Worker Railway services (both environments):

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://seq.railway.internal:5341/ingest/otlp
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

If you enabled authentication in SEQ (recommended for production), also set:

```
OTEL_EXPORTER_OTLP_HEADERS=X-Seq-ApiKey=<api-key-from-seq-ui>
```

Generate the API key in the SEQ web UI → **Settings → API Keys → Add API Key**.
Set the token permission to **Ingest only** to limit blast radius.

### 6 — Verify

Restart the Api and Worker services on Railway. Within a few seconds, events appear in the SEQ
web UI at the domain from step 3. Use the search bar to filter by service:

```
@ServiceName = 'tradeflow-api'
@ServiceName = 'tradeflow-worker'
```

---

## Useful SEQ queries

```
# All errors across both services
@Level = 'Error'

# A specific signal across both services
IdempotencyKey = 'USDJPY_1744329600'

# Filter rejections
FilterResult.IsAllowed = false

# Worker execution for one instrument
Instrument = 'USD_JPY' and @ServiceName = 'tradeflow-worker'
```

---

## Retention

SEQ's free tier retains logs indefinitely on disk. Set a retention policy in
**Settings → Retention Policies** to avoid unbounded disk growth on Railway.
A 7-day or 30-day policy is sufficient for most debugging needs.

---

## Volume / persistence

Railway volumes are mounted at `/data` inside the SEQ container. Attach a Railway volume
(project → **Volumes** tab → **Add Volume**) and set the mount path to `/data`.
Without a volume, logs are lost on redeploy.
