#!/usr/bin/env bash
set -euo pipefail

search_root="${1:-.}"

passed=0
failed=0
skipped=0
reports=0

while IFS= read -r trx; do
  reports=$((reports + 1))
  p="$(sed -n 's/.*passed="\([0-9]\+\)".*/\1/p' "$trx" | head -n1)"
  f="$(sed -n 's/.*failed="\([0-9]\+\)".*/\1/p' "$trx" | head -n1)"
  ne="$(sed -n 's/.*notExecuted="\([0-9]\+\)".*/\1/p' "$trx" | head -n1)"
  nr="$(sed -n 's/.*notRunnable="\([0-9]\+\)".*/\1/p' "$trx" | head -n1)"
  [[ -z "$p" ]] && p=0
  [[ -z "$f" ]] && f=0
  [[ -z "$ne" ]] && ne=0
  [[ -z "$nr" ]] && nr=0
  passed=$((passed + p))
  failed=$((failed + f))
  skipped=$((skipped + ne + nr))
done < <(find "$search_root" -type f -name "*.trx" 2>/dev/null | sort || true)

echo "passed=$passed"
echo "failed=$failed"
echo "skipped=$skipped"
echo "reports=$reports"
