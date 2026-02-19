param(
    [string]$ContractPath = "docs/30-contracts/test-gate.contract.yaml",
    [string]$CoverageSearchRoot = "tests",
    [string]$CoverageFileName = "coverage.cobertura.xml"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepoPath {
    param([string]$PathValue)

    return $PathValue.Replace("\", "/").Trim().TrimStart("./")
}

function Parse-TestGateContract {
    param([string]$PathValue)

    if (-not (Test-Path $PathValue)) {
        throw "Contract file not found: $PathValue"
    }

    $lineMin = $null
    $branchMin = $null
    $criticalFiles = New-Object System.Collections.Generic.List[string]
    $requiredTests = New-Object System.Collections.Generic.List[string]

    $section = ""
    foreach ($rawLine in Get-Content $PathValue) {
        $trimmed = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#", [System.StringComparison]::Ordinal)) {
            continue
        }

        if ($trimmed -eq "coverage:") {
            $section = "coverage"
            continue
        }

        if ($trimmed -eq "scenarios:") {
            $section = "scenarios"
            continue
        }

        if ($trimmed -eq "criticalFiles:") {
            $section = "criticalFiles"
            continue
        }

        if ($trimmed -eq "requiredTests:") {
            $section = "requiredTests"
            continue
        }

        if ($section -eq "coverage" -and $trimmed -match "^lineMin:\s*([0-9]*\.?[0-9]+)$") {
            $lineMin = [double]::Parse($Matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
            continue
        }

        if ($section -eq "coverage" -and $trimmed -match "^branchMin:\s*([0-9]*\.?[0-9]+)$") {
            $branchMin = [double]::Parse($Matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
            continue
        }

        if ($section -eq "criticalFiles" -and $trimmed -match "^- (.+)$") {
            $criticalFiles.Add((Normalize-RepoPath -PathValue $Matches[1]))
            continue
        }

        if ($section -eq "requiredTests" -and $trimmed -match "^- (.+)$") {
            $requiredTests.Add($Matches[1].Trim())
            continue
        }
    }

    if ($null -eq $lineMin -or $null -eq $branchMin) {
        throw "Missing coverage thresholds in contract: $PathValue"
    }

    if ($criticalFiles.Count -eq 0) {
        throw "No critical files configured in contract: $PathValue"
    }

    return [pscustomobject]@{
        LineMin = $lineMin
        BranchMin = $branchMin
        CriticalFiles = @($criticalFiles)
        RequiredTests = @($requiredTests)
    }
}

function Parse-ConditionCoverage {
    param([string]$ConditionCoverage)

    if ([string]::IsNullOrWhiteSpace($ConditionCoverage)) {
        return $null
    }

    if ($ConditionCoverage -match "\((\d+)\/(\d+)\)") {
        return [pscustomobject]@{
            Covered = [int]$Matches[1]
            Total = [int]$Matches[2]
        }
    }

    return $null
}

function Assert-RequiredTestsExist {
    param([string[]]$RequiredTests)

    if ($RequiredTests.Count -eq 0) {
        return
    }

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $RequiredTests) {
        $parts = $entry.Split("::", 2, [System.StringSplitOptions]::None)
        if ($parts.Length -ne 2) {
            $missing.Add("$entry (invalid contract format; expected path::Method)")
            continue
        }

        $filePath = $parts[0]
        $methodName = $parts[1]
        if (-not (Test-Path $filePath)) {
            $missing.Add("$entry (file not found)")
            continue
        }

        $pattern = "void\s+$([regex]::Escape($methodName))\s*\("
        if (-not (Select-String -Path $filePath -Pattern $pattern -Quiet)) {
            $missing.Add("$entry (method not found)")
        }
    }

    if ($missing.Count -gt 0) {
        Write-Host "Missing required scenario tests:"
        foreach ($entry in $missing) {
            Write-Host " - $entry"
        }
        throw "Required scenario test checks failed."
    }
}

$contract = Parse-TestGateContract -PathValue $ContractPath
Assert-RequiredTestsExist -RequiredTests $contract.RequiredTests

$coverageFiles = @(Get-ChildItem -Path $CoverageSearchRoot -Recurse -File -Filter $CoverageFileName | Select-Object -ExpandProperty FullName)
if ($coverageFiles.Count -eq 0) {
    throw "No coverage files found under '$CoverageSearchRoot' with name '$CoverageFileName'."
}

$criticalSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($path in $contract.CriticalFiles) {
    [void]$criticalSet.Add((Normalize-RepoPath -PathValue $path))
}

$lineHits = @{}
$branchHits = @{}
$seenClasses = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($coveragePath in $coverageFiles) {
    [xml]$coverage = Get-Content $coveragePath
    foreach ($class in $coverage.coverage.packages.package.classes.class) {
        $filename = Normalize-RepoPath -PathValue ([string]$class.filename)
        $matchedCritical = $null
        foreach ($criticalFile in $criticalSet) {
            if ($filename.EndsWith($criticalFile, [System.StringComparison]::OrdinalIgnoreCase)) {
                $matchedCritical = $criticalFile
                break
            }
        }

        if ($null -eq $matchedCritical) {
            continue
        }

        [void]$seenClasses.Add($matchedCritical)

        foreach ($line in $class.lines.line) {
            $lineNumber = [int]$line.number
            $lineKey = "$matchedCritical|$lineNumber"
            $covered = ([int]$line.hits) -gt 0
            if (-not $lineHits.ContainsKey($lineKey)) {
                $lineHits[$lineKey] = $false
            }
            if ($covered) {
                $lineHits[$lineKey] = $true
            }

            if ([string]$line.branch -ne "True") {
                continue
            }

            $parsed = Parse-ConditionCoverage -ConditionCoverage ([string]$line.'condition-coverage')
            if ($null -eq $parsed) {
                continue
            }

            $branchKey = "$matchedCritical|$lineNumber"
            if (-not $branchHits.ContainsKey($branchKey)) {
                $branchHits[$branchKey] = [pscustomobject]@{
                    Covered = 0
                    Total = 0
                }
            }

            $existing = $branchHits[$branchKey]
            $branchHits[$branchKey] = [pscustomobject]@{
                Covered = [Math]::Max([int]$existing.Covered, [int]$parsed.Covered)
                Total = [Math]::Max([int]$existing.Total, [int]$parsed.Total)
            }
        }
    }
}

if ($seenClasses.Count -eq 0) {
    throw "No critical files from contract were found in coverage reports."
}

$missingCoverageFiles = New-Object System.Collections.Generic.List[string]
foreach ($criticalFile in $criticalSet) {
    if (-not $seenClasses.Contains($criticalFile)) {
        $missingCoverageFiles.Add($criticalFile)
    }
}

if ($missingCoverageFiles.Count -gt 0) {
    Write-Host "Critical files missing from coverage data:"
    foreach ($path in $missingCoverageFiles) {
        Write-Host " - $path"
    }
    throw "Coverage report does not include all critical files."
}

$lineValid = $lineHits.Count
$lineCovered = ($lineHits.Values | Where-Object { $_ }).Count
$lineRate = if ($lineValid -eq 0) { 0.0 } else { $lineCovered / $lineValid }

$branchValid = 0
$branchCovered = 0
foreach ($entry in $branchHits.Values) {
    $branchValid += [int]$entry.Total
    $branchCovered += [int]$entry.Covered
}
$branchRate = if ($branchValid -eq 0) { 1.0 } else { $branchCovered / $branchValid }

Write-Host "Critical coverage summary"
Write-Host " - Line:   $lineCovered / $lineValid = $([math]::Round($lineRate, 4))"
Write-Host " - Branch: $branchCovered / $branchValid = $([math]::Round($branchRate, 4))"
Write-Host " - Thresholds: line >= $($contract.LineMin), branch >= $($contract.BranchMin)"

$failed = $false
if ($lineRate -lt $contract.LineMin) {
    Write-Host "Line coverage threshold failed."
    $failed = $true
}

if ($branchRate -lt $contract.BranchMin) {
    Write-Host "Branch coverage threshold failed."
    $failed = $true
}

if ($failed) {
    throw "Critical coverage gate failed."
}

Write-Host "Critical coverage gate passed."
