#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="$repo_root/artifacts/skills"
version_suffix=""
if [[ -n "${VERSION:-}" ]]; then
  version_suffix="-$VERSION"
fi

mkdir -p "$output_dir"

shopt -s nullglob
for skill_dir in "$repo_root"/skills/*; do
  if [[ ! -d "$skill_dir" ]]; then
    continue
  fi
  if [[ ! -f "$skill_dir/SKILL.md" ]]; then
    continue
  fi
  name="$(basename "$skill_dir")"
  out="$output_dir/$name$version_suffix.skill"
  rm -f "$out"
  (cd "$repo_root" && zip -r -q "$out" "skills/$name")
  echo "Packaged $name -> $out"
done
