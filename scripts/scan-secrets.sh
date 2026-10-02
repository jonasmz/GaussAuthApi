#!/usr/bin/env bash
set -euo pipefail

# Only locations are printed: CI output remains safe even when a finding exists.
found=0
report() { printf 'secret-hygiene finding: %s\n' "$1" >&2; found=1; }
is_candidate() { [[ "$1" =~ (^|/)(\.env[^/]*|.*\.(json|ya?ml|env|config))$ || "$1" =~ (compose|Dockerfile) ]]; }
has_secret() {
  local file="$1"
  grep -Eqi -- '-----BEGIN ([A-Z ]+ )?PRIVATE KEY-----' "$file" && return 0
  grep -Eqi -- '(Host|Server)=[^;[:space:]]+.*(Password|Pwd)=[^;[:space:]]+' "$file" && \
    ! grep -Eqi -- '(Password|Pwd)=(replace_with_[A-Za-z0-9_]+|not_a_secret|\$\{[^}]+\})([;[:space:]]|$)' "$file" && return 0
  grep -Eqi -- '^[[:space:]]*[^#[:space:]][^:=]*(secret|token|password|privatekey)[^:=]*[[:space:]]*[:=][[:space:]]*[A-Za-z0-9+/_=-]{32,}' "$file" && \
    ! grep -Eqi -- '^[[:space:]]*[^#[:space:]][^:=]*(secret|token|password|privatekey)[^:=]*[[:space:]]*[:=][[:space:]]*(replace_with_[A-Za-z0-9_]+|not_a_secret|\$\{[^}]+\})[[:space:]]*$' "$file" && return 0
  return 1
}

while IFS= read -r -d '' file; do
  is_candidate "$file" && has_secret "$file" && report "$file"
done < <(git ls-files -z)

# Search all reachable commits without emitting patches or matched values.
while IFS= read -r commit; do
  [[ -n "$commit" ]] && report "history:$commit"
done < <(git log --all --format='%H' -G '-----BEGIN ([A-Z ]+ )?PRIVATE KEY-----|((Host|Server)=[^;[:space:]]+.*(Password|Pwd)=[^$;[:space:]])' -- .env.example 'compose*.yml' 'compose*.yaml' '*.json')

[[ "$found" -eq 0 ]]
