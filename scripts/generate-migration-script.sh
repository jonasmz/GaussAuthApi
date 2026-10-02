#!/usr/bin/env bash
# Generates the idempotent SQL script for the complete EF Core migration chain.
#
# The script is the reviewed-alternative to the `migrate` command: it represents exactly the migrations in this
# checkout, applies only what is missing (safe to run repeatedly), and contains no credentials. It is never hand-edited.
#
# Run inside the development SDK container (the host needs no .NET SDK):
#   docker compose -f compose.dev.yml exec sdk scripts/generate-migration-script.sh [output-file]
# Default output: artifacts/gaussauth-schema.sql
set -euo pipefail

cd "$(dirname "$0")/.."
output="${1:-artifacts/gaussauth-schema.sql}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: the .NET SDK was not found. Run this script inside the development SDK container:" >&2
  echo "  docker compose -f compose.dev.yml exec sdk scripts/generate-migration-script.sh" >&2
  exit 1
fi

if ! dotnet ef --version >/dev/null 2>&1; then
  echo "dotnet-ef is not restored; running 'dotnet tool restore'..." >&2
  dotnet tool restore >/dev/null
fi

mkdir -p "$(dirname "$output")"
dotnet ef migrations script \
  --idempotent \
  --project src/GaussAuth.Infrastructure \
  --startup-project src/GaussAuth.Api \
  --configuration Release \
  --output "$output"

# EF writes a UTF-8 byte-order mark; psql rejects it as part of the first statement, so strip it.
sed -i '1s/^\xEF\xBB\xBF//' "$output"

# Defense in depth: a generated script must never carry connection settings.
if grep -qE 'Host=|Username=|Password=' "$output"; then
  echo "error: the generated script contains connection-string material; refusing to keep it." >&2
  rm -f "$output"
  exit 1
fi

echo "Wrote $output ($(grep -c 'EFMigrationsHistory' "$output") history references, $(wc -l < "$output") lines)."
