[CmdletBinding()]
param(
    [string]$RepoRoot = ".",
    [string[]]$Profiles = @("core"),
    [switch]$SyncNavigation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "Test-SpecTraceRepository.ps1") @PSBoundParameters
