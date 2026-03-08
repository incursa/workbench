#!/usr/bin/env bash
set -euo pipefail

configuration="Release"
results_directory="artifacts/quality/raw/test-results"
coverage_directory="artifacts/quality/raw/coverage"
no_build=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      configuration="$2"
      shift 2
      ;;
    --results-directory)
      results_directory="$2"
      shift 2
      ;;
    --coverage-directory)
      coverage_directory="$2"
      shift 2
      ;;
    --no-build)
      no_build=1
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd "$script_dir/../.." && pwd -P)"
results_path="$repo_root/$results_directory"
coverage_path="$repo_root/$coverage_directory"

rm -rf "$results_path" "$coverage_path"
mkdir -p "$results_path" "$coverage_path"

if [[ "$no_build" -eq 0 ]]; then
  dotnet build "$repo_root/Workbench.slnx" --configuration "$configuration"
fi

run_project() {
  local name="$1"
  local project_path="$2"

  dotnet test --project "$repo_root/$project_path" \
    --configuration "$configuration" \
    --no-build \
    --results-directory "$results_path" \
    --report-trx \
    --report-trx-filename "$name.trx" \
    -- \
    --coverage \
    --coverage-output-format cobertura \
    --coverage-output "$coverage_path/$name.coverage.cobertura.xml"
}

run_project "workbench-tests" "tests/Workbench.Tests/Workbench.Tests.csproj"
run_project "workbench-integration" "tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj"

echo "Quality evidence raw artifacts"
echo " - Test results: $results_path"
echo " - Coverage:     $coverage_path"
