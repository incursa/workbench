#!/usr/bin/env bash

status_from_outcome() {
  local outcome="${1:-}"
  if [[ "$outcome" == "success" ]]; then
    echo "✅"
  elif [[ "$outcome" == "failure" ]]; then
    echo "❌"
  else
    echo "⚠️"
  fi
}

format_percent_from_rate() {
  local rate="${1:-}"
  if [[ -z "$rate" ]]; then
    echo "n/a"
    return
  fi
  awk -v v="$rate" 'BEGIN { printf "%.2f%%", v * 100 }'
}

format_percent_from_value() {
  local value="${1:-}"
  if [[ -z "$value" ]]; then
    echo "n/a"
    return
  fi
  awk -v v="$value" 'BEGIN { printf "%.2f%%", v }'
}

format_delta_percent() {
  local current="${1:-}"
  local baseline="${2:-}"
  if [[ -z "$current" || -z "$baseline" ]]; then
    echo "n/a"
    return
  fi
  awk -v c="$current" -v b="$baseline" 'BEGIN { d=c-b; printf "%+.2f%% vs main", d }'
}

format_delta_int() {
  local current="${1:-}"
  local baseline="${2:-}"
  if [[ -z "$current" || -z "$baseline" ]]; then
    echo "n/a"
    return
  fi
  awk -v c="$current" -v b="$baseline" 'BEGIN { d=c-b; printf "%+d vs main", d }'
}
