<#
.SYNOPSIS
Validates canonical SpecTrace JSON artifacts.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = ".",
    [string[]]$Profiles = @("core"),
    [switch]$SyncNavigation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedRepoRoot = (Resolve-Path -LiteralPath $RepoRoot).ProviderPath
$toolRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).ProviderPath
$toolProject = Join-Path $toolRoot "src\Workbench\Workbench.csproj"
$toolDll = Join-Path $toolRoot "src\Workbench\bin\Release\net10.0\Workbench.dll"

if (-not (Test-Path -LiteralPath $toolProject)) {
    throw "Could not find Workbench project at '$toolProject'."
}

Push-Location $toolRoot
try {
    & dotnet build $toolProject -c Release -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "Workbench build failed."
    }

    if (-not (Test-Path -LiteralPath $toolDll)) {
        throw "Could not find built Workbench CLI at '$toolDll'."
    }
}
finally {
    Pop-Location
}

Push-Location $resolvedRepoRoot
try {
    foreach ($profile in $Profiles) {
        if ([string]::IsNullOrWhiteSpace($profile)) {
            continue
        }

        Write-Host "Running Workbench validation profile '$profile' in '$resolvedRepoRoot'..."
        & dotnet $toolDll validate --profile $profile
        if ($LASTEXITCODE -ne 0) {
            throw "Workbench validation failed for profile '$profile'."
        }
    }

    if ($SyncNavigation) {
        Write-Host "Refreshing generated navigation in '$resolvedRepoRoot'..."
        & dotnet $toolDll nav sync
        if ($LASTEXITCODE -ne 0) {
            throw "Navigation sync failed."
        }
    }
}
finally {
    Pop-Location
}
