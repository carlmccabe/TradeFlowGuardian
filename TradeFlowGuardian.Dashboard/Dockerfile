# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# ── Stage 2: serve ────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/nginx.conf.template

# Railway injects $PORT at runtime.
# $API_URL must be set to the TradeFlowGuardian API service URL
#   e.g. https://your-api.railway.app
#
# envsubst '$PORT $API_URL' substitutes only those two variables — nginx
# variables ($uri, $proxy_host, etc.) are intentionally left untouched.
CMD ["/bin/sh", "-c", \
  "envsubst '$PORT $API_URL' < /etc/nginx/nginx.conf.template > /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]
