<#
.SYNOPSIS
Ensures the pinned CUE CLI used by this repository is available.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$RootPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [Parameter()]
    [string]$Version,

    [Parameter()]
    [switch]$PopulateBundledAssets
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedRoot = (Resolve-Path -LiteralPath $RootPath).Path
$toolDirectory = Join-Path $resolvedRoot '.tools\cue\bin'
$downloadDirectory = Join-Path $resolvedRoot '.tools\cue\downloads'
$localCue = Join-Path $toolDirectory $(if ($IsWindows) { 'cue.exe' } else { 'cue' })
$bundledVersionPath = Join-Path $resolvedRoot 'src\Workbench.Core\Tooling\Cue\version.txt'
$bundledAssetsRoot = Join-Path $resolvedRoot 'src\Workbench.Core\Tooling\Cue\runtimes'
$releaseHeaders = @{
    Accept       = 'application/vnd.github+json'
    'User-Agent' = 'incursa-workbench'
}
$supportedTargets = @(
    @{ Rid = 'win-x64'; Os = 'windows'; Arch = 'amd64'; Extension = 'zip'; Binary = 'cue.exe' },
    @{ Rid = 'win-arm64'; Os = 'windows'; Arch = 'arm64'; Extension = 'zip'; Binary = 'cue.exe' },
    @{ Rid = 'linux-x64'; Os = 'linux'; Arch = 'amd64'; Extension = 'tar.gz'; Binary = 'cue' },
    @{ Rid = 'linux-arm64'; Os = 'linux'; Arch = 'arm64'; Extension = 'tar.gz'; Binary = 'cue' },
    @{ Rid = 'osx-x64'; Os = 'darwin'; Arch = 'amd64'; Extension = 'tar.gz'; Binary = 'cue' },
    @{ Rid = 'osx-arm64'; Os = 'darwin'; Arch = 'arm64'; Extension = 'tar.gz'; Binary = 'cue' }
)
$script:cueRelease = $null

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path -LiteralPath $bundledVersionPath) {
        $Version = (Get-Content -LiteralPath $bundledVersionPath -Raw).Trim()
    }
    else {
        $Version = 'v0.16.0'
    }
}

function Test-CueVersion {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$ExpectedVersion
    )

    try {
        $versionOutput = & $Path version 2>$null
        return $LASTEXITCODE -eq 0 -and ($versionOutput -match "cue version $([regex]::Escape($ExpectedVersion))")
    }
    catch {
        return $false
    }
}

function Get-CueReleaseTargetForCurrentHost {
    $osToken = if ($IsWindows) {
        'windows'
    }
    elseif ($IsMacOS) {
        'darwin'
    }
    elseif ($IsLinux) {
        'linux'
    }
    else {
        throw "Unsupported operating system for automatic CUE download."
    }

    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $archToken = switch ($architecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { 'amd64' }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { 'arm64' }
        default { throw "Unsupported architecture '$architecture' for automatic CUE download." }
    }

    $target = $supportedTargets |
        Where-Object { $_.Os -eq $osToken -and $_.Arch -eq $archToken } |
        Select-Object -First 1

    if ($null -eq $target) {
        throw "Unsupported CUE target '$osToken/$archToken'."
    }

    return $target
}

function Get-CueReleaseAssetName {
    param(
        [Parameter(Mandatory)]
        [string]$PinnedVersion,

        [Parameter(Mandatory)]
        [hashtable]$Target
    )

    return "cue_${PinnedVersion}_$($Target.Os)_$($Target.Arch).$($Target.Extension)"
}

function Get-CueRelease {
    param(
        [Parameter(Mandatory)]
        [string]$PinnedVersion
    )

    if ($null -eq $script:cueRelease) {
        $script:cueRelease = Invoke-RestMethod `
            -Uri "https://api.github.com/repos/cue-lang/cue/releases/tags/$PinnedVersion" `
            -Headers $releaseHeaders
    }

    return $script:cueRelease
}

function Get-CueReleaseAsset {
    param(
        [Parameter(Mandatory)]
        [string]$PinnedVersion,

        [Parameter(Mandatory)]
        [hashtable]$Target
    )

    $assetName = Get-CueReleaseAssetName -PinnedVersion $PinnedVersion -Target $Target
    $release = Get-CueRelease -PinnedVersion $PinnedVersion
    $asset = $release.assets |
        Where-Object { $_.name -eq $assetName } |
        Select-Object -First 1

    if ($null -eq $asset) {
        throw "Could not find CUE asset '$assetName' in release '$PinnedVersion'."
    }

    return $asset
}

function Expand-CueArchive {
    param(
        [Parameter(Mandatory)]
        [string]$ArchivePath,

        [Parameter(Mandatory)]
        [string]$DestinationPath
    )

    if ($ArchivePath.EndsWith('.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $DestinationPath -Force
        return
    }

    if ($ArchivePath.EndsWith('.tar.gz', [System.StringComparison]::OrdinalIgnoreCase)) {
        & tar -xzf $ArchivePath -C $DestinationPath
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to extract '$ArchivePath' with tar."
        }

        return
    }

    throw "Unsupported archive format '$ArchivePath'."
}

function Get-RequiredCueBinary {
    param(
        [Parameter(Mandatory)]
        [string]$SearchRoot,

        [Parameter(Mandatory)]
        [string]$BinaryName
    )

    $candidate = Get-ChildItem -Path $SearchRoot -Recurse -File -Filter $BinaryName |
        Select-Object -First 1

    if ($null -eq $candidate) {
        throw "The downloaded archive did not contain '$BinaryName'."
    }

    return $candidate.FullName
}

function Get-CueArchivePath {
    param(
        [Parameter(Mandatory)]
        [psobject]$Asset
    )

    New-Item -ItemType Directory -Force -Path $downloadDirectory | Out-Null
    $archivePath = Join-Path $downloadDirectory $Asset.name
    if (-not (Test-Path -LiteralPath $archivePath)) {
        Invoke-WebRequest `
            -Uri $Asset.browser_download_url `
            -Headers @{ 'User-Agent' = 'incursa-workbench' } `
            -OutFile $archivePath
    }

    return $archivePath
}

function Get-CueExtractRoot {
    param(
        [Parameter(Mandatory)]
        [psobject]$Asset
    )

    $extractFolderName = if ($Asset.name.EndsWith('.tar.gz', [System.StringComparison]::OrdinalIgnoreCase)) {
        $Asset.name.Substring(0, $Asset.name.Length - '.tar.gz'.Length)
    }
    else {
        [System.IO.Path]::GetFileNameWithoutExtension($Asset.name)
    }

    return Join-Path $downloadDirectory $extractFolderName
}

function Expand-CueAsset {
    param(
        [Parameter(Mandatory)]
        [psobject]$Asset
    )

    $archivePath = Get-CueArchivePath -Asset $Asset
    $extractRoot = Get-CueExtractRoot -Asset $Asset
    if (Test-Path -LiteralPath $extractRoot) {
        $resolvedExtractRoot = (Resolve-Path -LiteralPath $extractRoot).Path
        $resolvedDownloadRoot = (Resolve-Path -LiteralPath $downloadDirectory).Path
        if (-not $resolvedExtractRoot.StartsWith($resolvedDownloadRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean extraction path '$resolvedExtractRoot' because it is outside '$resolvedDownloadRoot'."
        }

        Remove-Item -LiteralPath $resolvedExtractRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    Expand-CueArchive -ArchivePath $archivePath -DestinationPath $extractRoot
    return $extractRoot
}

function Install-CueBinary {
    param(
        [Parameter(Mandatory)]
        [hashtable]$Target,

        [Parameter(Mandatory)]
        [string]$DestinationPath
    )

    $asset = Get-CueReleaseAsset -PinnedVersion $Version -Target $Target
    $extractRoot = Expand-CueAsset -Asset $asset
    $downloadedCue = Get-RequiredCueBinary -SearchRoot $extractRoot -BinaryName $Target.Binary
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationPath) | Out-Null
    Copy-Item -LiteralPath $downloadedCue -Destination $DestinationPath -Force
    return $DestinationPath
}

if ($PopulateBundledAssets) {
    foreach ($target in $supportedTargets) {
        $destinationPath = Join-Path $bundledAssetsRoot "$($target.Rid)\native\$($target.Binary)"
        $installedPath = Install-CueBinary -Target $target -DestinationPath $destinationPath
        Write-Output $installedPath
    }

    exit 0
}

if (Test-Path -LiteralPath $localCue) {
    if (Test-CueVersion -Path $localCue -ExpectedVersion $Version) {
        Write-Output $localCue
        exit 0
    }
}

$globalCue = Get-Command cue -ErrorAction SilentlyContinue
if ($null -ne $globalCue -and (Test-CueVersion -Path $globalCue.Source -ExpectedVersion $Version)) {
    Write-Output $globalCue.Source
    exit 0
}

$hostTarget = Get-CueReleaseTargetForCurrentHost
New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
$installedCue = Install-CueBinary -Target $hostTarget -DestinationPath $localCue

if (-not (Test-Path -LiteralPath $installedCue)) {
    throw "Expected CUE CLI at '$localCue' after download, but it was not found."
}

if (-not (Test-CueVersion -Path $installedCue -ExpectedVersion $Version)) {
    throw "Downloaded CUE CLI at '$localCue' does not report version '$Version'."
}

Write-Output $installedCue
