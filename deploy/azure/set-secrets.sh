#!/usr/bin/env bash
# Interactive: asks for the Postgres and SMTP passwords on the VM, so they are typed only here.
#   sudo PG_HOST=<server>.postgres.database.azure.com PG_USER=<admin> bash set-secrets.sh
# Creates/updates:
#   * k8s secret fundoo/fundoo-secrets (connection strings, JWT key, SMTP, Google SSO placeholders)
#   * Jenkins credential "azure-pg" (via /etc/jenkins-casc.env, applied on Jenkins restart)
#   * k8s secret fundoo/signoz-backend (where the in-cluster OpenTelemetry Collector sends telemetry)
# Re-run any time to change values; the JWT signing key and internal secret are kept.
# Only (re)point telemetry at SigNoz, without touching the other secrets:
#   sudo SIGNOZ_ONLY=1 bash set-secrets.sh
set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

# SigNoz backend for deploy/k8s/30-observability.yaml. Not a password, but environment-specific, so it
# lives in a secret next to the others instead of in Git.
#   separate SigNoz VM : <private-ip>:4317, insecure=true, no key
#   SigNoz Cloud       : ingest.<region>.signoz.cloud:443, insecure=false, ingestion key
set_signoz() {
  local cur; cur=$(kubectl -n fundoo get secret signoz-backend -o 'jsonpath={.data.SIGNOZ_OTLP_ENDPOINT}' 2>/dev/null | base64 -d 2>/dev/null || true)
  read -rp  "SigNoz OTLP gRPC endpoint host:port [${cur:-skip}]: " SIGNOZ_EP; SIGNOZ_EP=${SIGNOZ_EP:-$cur}
  [ -n "$SIGNOZ_EP" ] || { echo "SigNoz endpoint not set; telemetry stays in the collector (dropped)."; return 0; }
  read -rp  "Plain-text connection (private network, no TLS)? [Y/n]: " SIGNOZ_PLAIN
  case "${SIGNOZ_PLAIN:-y}" in [Nn]*) SIGNOZ_INSECURE=false;; *) SIGNOZ_INSECURE=true;; esac
  read -rsp "SigNoz ingestion key (SigNoz Cloud only, blank for self-hosted): " SIGNOZ_KEY; echo
  kubectl create namespace fundoo --dry-run=client -o yaml | kubectl apply -f - >/dev/null
  kubectl -n fundoo create secret generic signoz-backend \
    --from-literal=SIGNOZ_OTLP_ENDPOINT="$SIGNOZ_EP" \
    --from-literal=SIGNOZ_INSECURE="$SIGNOZ_INSECURE" \
    --from-literal=SIGNOZ_INGESTION_KEY="$SIGNOZ_KEY" \
    --dry-run=client -o yaml | kubectl apply -f - >/dev/null
  kubectl -n fundoo rollout restart deployment otel-collector >/dev/null 2>&1 || true
  echo "k8s secret signoz-backend updated (collector restarting)"
}

if [ "${SIGNOZ_ONLY:-}" = 1 ]; then set_signoz; exit 0; fi

: "${PG_HOST:?PG_HOST is required}" "${PG_USER:?PG_USER is required}"

read -rsp "Postgres password for $PG_USER: " PG_PASS; echo
case "$PG_PASS" in *"'"*) echo "Password must not contain a single quote"; exit 1;; esac
read -rp  "SMTP host [smtp.gmail.com]: " SMTP_HOST; SMTP_HOST=${SMTP_HOST:-smtp.gmail.com}
read -rp  "SMTP port [587]: " SMTP_PORT; SMTP_PORT=${SMTP_PORT:-587}
read -rp  "SMTP username (e.g. you@gmail.com): " SMTP_USER
read -rsp "SMTP password (Gmail: an App Password): " SMTP_PASS; echo
read -rp  "Sender email [$SMTP_USER]: " SMTP_FROM; SMTP_FROM=${SMTP_FROM:-$SMTP_USER}

echo "Checking Postgres and creating databases if missing..."
for db in fundoo_users fundoo_notes; do
  docker run --rm -e PGPASSWORD="$PG_PASS" -e PGSSLMODE=require postgres:16-alpine \
    psql -h "$PG_HOST" -U "$PG_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$db'" | grep -q 1 \
  || docker run --rm -e PGPASSWORD="$PG_PASS" -e PGSSLMODE=require postgres:16-alpine \
    psql -h "$PG_HOST" -U "$PG_USER" -d postgres -c "CREATE DATABASE $db"
done
echo "Postgres OK (fundoo_users, fundoo_notes)"

keep() { kubectl -n fundoo get secret fundoo-secrets -o "jsonpath={.data.$1}" 2>/dev/null | base64 -d 2>/dev/null || true; }
JWT=$(keep Jwt__SigningKey);               JWT=${JWT:-$(openssl rand -base64 48)}
INTERNAL=$(keep InternalService__SharedSecret); INTERNAL=${INTERNAL:-$(openssl rand -base64 48)}

CS="Host=$PG_HOST;Port=5432;Username=$PG_USER;Password='$PG_PASS';Ssl Mode=Require"
kubectl create namespace fundoo --dry-run=client -o yaml | kubectl apply -f - >/dev/null
kubectl -n fundoo create secret generic fundoo-secrets \
  --from-literal=ConnectionStrings__UserDb="$CS;Database=fundoo_users" \
  --from-literal=ConnectionStrings__NotesDb="$CS;Database=fundoo_notes" \
  --from-literal=Jwt__SigningKey="$JWT" \
  --from-literal=InternalService__SharedSecret="$INTERNAL" \
  --from-literal=EmailSettings__Host="$SMTP_HOST" --from-literal=EmailSettings__Port="$SMTP_PORT" \
  --from-literal=EmailSettings__SenderEmail="$SMTP_FROM" --from-literal=EmailSettings__SenderName=Fundoo \
  --from-literal=EmailSettings__Username="$SMTP_USER" --from-literal=EmailSettings__Password="$SMTP_PASS" \
  --from-literal=Authentication__Google__ClientId= --from-literal=Authentication__Google__ClientSecret= \
  --dry-run=client -o yaml | kubectl apply -f - >/dev/null
echo "k8s secret fundoo-secrets updated"

# Jenkins "azure-pg" credential (JCasC reads it from this env file at startup).
if [ -f /etc/jenkins-casc.env ]; then
  grep -v '^PG_PASS=\|^PG_USER=' /etc/jenkins-casc.env > /etc/jenkins-casc.env.new
  printf 'PG_USER=%s\nPG_PASS=%s\n' "$PG_USER" "$PG_PASS" >> /etc/jenkins-casc.env.new
  install -m 600 -o root -g root /etc/jenkins-casc.env.new /etc/jenkins-casc.env && rm /etc/jenkins-casc.env.new
  systemctl restart jenkins
  echo "Jenkins credential azure-pg updated (Jenkins restarting)"
fi

set_signoz

# Restart running services so they pick up the new secret (no-op before the first deploy).
kubectl -n fundoo rollout restart deployment user-api notes-api notification-api >/dev/null 2>&1 || true
echo "Done."
