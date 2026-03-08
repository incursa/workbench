param(
    [string]$Configuration = "Release",
    [string]$ResultsDirectory = "artifacts/quality/raw/test-results",
    [string]$CoverageDirectory = "artifacts/quality/raw/coverage",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [string]$BasePath,
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$solutionPath = Join-Path $repoRoot "Workbench.slnx"
$resultsPath = Resolve-AbsolutePath -BasePath $repoRoot -PathValue $ResultsDirectory
$coveragePath = Resolve-AbsolutePath -BasePath $repoRoot -PathValue $CoverageDirectory

$projects = @(
    [pscustomobject]@{
        Name = "workbench-tests"
        Path = Join-Path $repoRoot "tests/Workbench.Tests/Workbench.Tests.csproj"
    },
    [pscustomobject]@{
        Name = "workbench-integration"
        Path = Join-Path $repoRoot "tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj"
    }
)

if (Test-Path $resultsPath) {
    Remove-Item -Recurse -Force $resultsPath
}

if (Test-Path $coveragePath) {
    Remove-Item -Recurse -Force $coveragePath
}

[void](New-Item -ItemType Directory -Force -Path $resultsPath)
[void](New-Item -ItemType Directory -Force -Path $coveragePath)

if (-not $NoBuild) {
    dotnet build $solutionPath --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

foreach ($project in $projects) {
    $coverageFile = Join-Path $coveragePath "$($project.Name).coverage.cobertura.xml"

    dotnet test --project $project.Path `
        --configuration $Configuration `
        --no-build `
        --results-directory $resultsPath `
        --report-trx `
        --report-trx-filename "$($project.Name).trx" `
        -- `
        --coverage `
        --coverage-output-format cobertura `
        --coverage-output $coverageFile

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "Quality evidence raw artifacts"
Write-Host " - Test results: $resultsPath"
Write-Host " - Coverage:     $coveragePath"
