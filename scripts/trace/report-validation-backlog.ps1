[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/validation"
)

$ErrorActionPreference = "Stop"
$repoPath = [System.IO.Path]::GetFullPath($RepoRoot)
$projectPath = Join-Path $repoPath "src/Workbench/Workbench.csproj"
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoPath $OutputDirectory
}

& dotnet build $projectPath --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Workbench build failed with exit code $LASTEXITCODE."
}

$findings = [System.Collections.Generic.List[object]]::new()
$profileResults = [System.Collections.Generic.List[object]]::new()

foreach ($profile in @("traceable", "auditable")) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @(
            "run",
            "--project", $projectPath,
            "--configuration", $Configuration,
            "--no-build",
            "--no-launch-profile",
            "--",
            "--format", "json",
            "validate",
            "--profile", $profile)) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ([string]::IsNullOrWhiteSpace($standardOutput)) {
        throw "Validation profile '$profile' produced no JSON. $standardError"
    }

    $payload = $standardOutput | ConvertFrom-Json -Depth 32
    if ($null -eq $payload.data) {
        throw "Validation profile '$profile' did not return the expected data envelope. $standardError"
    }

    foreach ($finding in @($payload.data.findings)) {
        if ($null -eq $finding) {
            continue
        }

        $relativeFile = if ([System.IO.Path]::IsPathRooted($finding.file)) {
            [System.IO.Path]::GetRelativePath($repoPath, $finding.file)
        } else {
            $finding.file
        }
        $relativeFile = $relativeFile.Replace('\', '/')
        $artifactKind = "other"
        $domain = "other"
        if ($relativeFile -match '^specs/(?<kind>[^/]+)/(?<domain>[^/]+)/') {
            $artifactKind = $Matches.kind
            $domain = $Matches.domain
        }
        $relativeTargetFile = $finding.targetFile
        if (-not [string]::IsNullOrWhiteSpace($relativeTargetFile)) {
            if ([System.IO.Path]::IsPathRooted($relativeTargetFile)) {
                $relativeTargetFile = [System.IO.Path]::GetRelativePath($repoPath, $relativeTargetFile)
            }
            $relativeTargetFile = $relativeTargetFile.Replace('\', '/')
        }

        $findings.Add([pscustomobject][ordered]@{
                profile      = $profile
                category     = $finding.category
                severity     = $finding.severity
                domain       = $domain
                artifactKind = $artifactKind
                file         = $relativeFile
                artifactId   = $finding.artifactId
                field        = $finding.field
                targetId     = $finding.targetId
                targetType   = $finding.targetType
                targetFile   = $relativeTargetFile
                message      = $finding.message
            })
    }

    $profileResults.Add([pscustomobject][ordered]@{
            profile  = $profile
            exitCode = $process.ExitCode
            ok       = $payload.ok
        })
}

$sortedFindings = @($findings | Sort-Object profile, category, domain, file, artifactId, targetId)
$summaries = foreach ($profileResult in $profileResults) {
    $profileFindings = @($sortedFindings | Where-Object profile -eq $profileResult.profile)
    [ordered]@{
        profile       = $profileResult.profile
        validationOk  = $profileResult.ok
        validationExitCode = $profileResult.exitCode
        total         = $profileFindings.Count
        categories    = @($profileFindings | Group-Object category | Sort-Object Name | ForEach-Object {
                [ordered]@{ name = $_.Name; count = $_.Count }
            })
        domains       = @($profileFindings | Group-Object domain | Sort-Object @{ Expression = "Count"; Descending = $true }, Name | ForEach-Object {
                [ordered]@{ name = $_.Name; count = $_.Count }
            })
        artifactKinds = @($profileFindings | Group-Object artifactKind | Sort-Object @{ Expression = "Count"; Descending = $true }, Name | ForEach-Object {
                [ordered]@{ name = $_.Name; count = $_.Count }
            })
    }
}

$report = [ordered]@{
    schemaVersion = 1
    summaries     = @($summaries)
    findings      = $sortedFindings
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$jsonPath = Join-Path $outputPath "trace-validation-backlog.json"
$markdownPath = Join-Path $outputPath "trace-validation-backlog.md"
$report | ConvertTo-Json -Depth 12 | Set-Content -Path $jsonPath -Encoding utf8

$markdown = [System.Text.StringBuilder]::new()
[void]$markdown.AppendLine("# Trace Validation Backlog")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('Generated from the current repository using the production `workbench validate` command. The JSON companion contains the deterministic, sorted finding backlog.')
foreach ($summary in $summaries) {
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("## $($summary.profile)")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("Total findings: $($summary.total)")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("| Category | Count |")
    [void]$markdown.AppendLine("| --- | ---: |")
    foreach ($category in $summary.categories) {
        [void]$markdown.AppendLine("| $($category.name) | $($category.count) |")
    }
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("| Domain | Count |")
    [void]$markdown.AppendLine("| --- | ---: |")
    foreach ($domain in $summary.domains) {
        [void]$markdown.AppendLine("| $($domain.name) | $($domain.count) |")
    }
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("| Artifact kind | Count |")
    [void]$markdown.AppendLine("| --- | ---: |")
    foreach ($kind in $summary.artifactKinds) {
        [void]$markdown.AppendLine("| $($kind.name) | $($kind.count) |")
    }
}
$markdown.ToString() | Set-Content -Path $markdownPath -Encoding utf8

Write-Host "Trace validation backlog written to:"
Write-Host "  $jsonPath"
Write-Host "  $markdownPath"
