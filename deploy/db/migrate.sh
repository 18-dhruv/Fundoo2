#!/usr/bin/env bash
# Runs all database migrations against PostgreSQL. Used by the Jenkins "DB migrations" stage,
# and can be run by hand from any machine with Docker that the Postgres firewall allows.
#
#   PG_HOST=<server>.postgres.database.azure.com PG_USER=<admin> PG_PASSWORD=<pass> deploy/db/migrate.sh
#
# 1. User DB  : EF Core migrations from Fundoo.User.Infrastructure (dotnet ef database update)
# 2. Notes DB : deploy/db/notes-db.sql (the Notes service uses raw SQL, no EF). Idempotent.
#
# Optional env: PG_PORT (5432), PG_SSLMODE (require), USER_DB (fundoo_users), NOTES_DB (fundoo_notes),
#               DOCKER_NETWORK (attach the tool containers to a docker network, for local testing).
# PG_PASSWORD must not contain a single quote (it is quoted inside the Npgsql connection string).
set -euo pipefail

: "${PG_HOST:?PG_HOST is required}" "${PG_USER:?PG_USER is required}" "${PG_PASSWORD:?PG_PASSWORD is required}"
PG_PORT="${PG_PORT:-5432}"
PG_SSLMODE="${PG_SSLMODE:-require}"
USER_DB="${USER_DB:-fundoo_users}"
NOTES_DB="${NOTES_DB:-fundoo_notes}"
EF_VERSION="8.0.11"   # matches Npgsql.EntityFrameworkCore.PostgreSQL / EF Core in the User service

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
NET_ARGS=()
[ -n "${DOCKER_NETWORK:-}" ] && NET_ARGS=(--network "$DOCKER_NETWORK")

# Npgsql spells SSL modes in PascalCase.
case "$PG_SSLMODE" in
  disable) NPGSQL_SSL=Disable ;; prefer) NPGSQL_SSL=Prefer ;; *) NPGSQL_SSL=Require ;;
esac
export ConnectionStrings__UserDb="Host=$PG_HOST;Port=$PG_PORT;Database=$USER_DB;Username=$PG_USER;Password='$PG_PASSWORD';Ssl Mode=$NPGSQL_SSL"

echo "==> [1/2] EF Core migrations -> $PG_HOST/$USER_DB"
# The repo is mounted read-only and copied inside the container, so the build never leaves
# root-owned bin/obj folders in the Jenkins workspace. Jwt/Redis values are placeholders: the User API's startup code requires them to build its host,
# but migrations never use them.
docker run --rm "${NET_ARGS[@]}" \
  -v "$REPO/src":/repo/src:ro \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -e ConnectionStrings__UserDb \
  -e Jwt__SigningKey=migration-placeholder-key-not-used-0123456789 \
  -e Jwt__Issuer=migrations -e Jwt__Audience=migrations \
  -e Redis__ConnectionString="localhost:6379,abortConnect=false" \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -c "set -e
    cp -r /repo /work && cd /work && find src -type d \\( -name bin -o -name obj \\) -prune -exec rm -rf {} +
    dotnet tool install dotnet-ef --version $EF_VERSION --tool-path /tmp/ef >/dev/null
    /tmp/ef/dotnet-ef migrations list \
      --project src/Services/User/Fundoo.User.Infrastructure \
      --startup-project src/Services/User/Fundoo.User.Api
    /tmp/ef/dotnet-ef database update \
      --project src/Services/User/Fundoo.User.Infrastructure \
      --startup-project src/Services/User/Fundoo.User.Api"

echo "==> [2/2] Notes schema -> $PG_HOST/$NOTES_DB"
export PGPASSWORD="$PG_PASSWORD"   # passed by name below so it never appears in the process list
docker run --rm -i "${NET_ARGS[@]}" \
  -e PGHOST="$PG_HOST" -e PGPORT="$PG_PORT" -e PGUSER="$PG_USER" -e PGPASSWORD \
  -e PGSSLMODE="$PG_SSLMODE" -e PGDATABASE="$NOTES_DB" \
  postgres:16-alpine psql -v ON_ERROR_STOP=1 -q < "$REPO/deploy/db/notes-db.sql"

echo "==> Migrations complete"
