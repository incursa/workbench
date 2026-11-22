param(
    [ValidateSet("All", "DotNet", "Mcp")]
    [string]$Mode = "All",
    [switch]$SkipRestore,
    [switch]$SkipFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "Workbench.slnx"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ">> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

Push-Location $repoRoot
try {
    if ($Mode -in @("All", "DotNet")) {
        if (-not $SkipRestore) {
            Invoke-Checked "dotnet" @("restore", $solutionPath)
            Invoke-Checked "dotnet" @("tool", "restore")
        }

        if (-not $SkipFormat) {
            Invoke-Checked "dotnet" @("format", $solutionPath, "--verify-no-changes", "--no-restore")
        }

        Invoke-Checked "dotnet" @(
            "build",
            $solutionPath,
            "--configuration", "Release",
            "--no-restore",
            "-warnaserror",
            "-p:ContinuousIntegrationBuild=true")
        Invoke-Checked "dotnet" @(
            "test",
            "--solution", $solutionPath,
            "--configuration", "Release",
            "--no-build")
    }

    if ($Mode -in @("All", "Mcp")) {
        if (-not $SkipRestore) {
            Invoke-Checked "npm" @("ci")
        }

        Invoke-Checked "npm" @("test")
        Invoke-Checked "npm" @("audit")
    }
}
finally {
    Pop-Location
}
