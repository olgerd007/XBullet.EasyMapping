[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
function Assert-Rejected([scriptblock] $Action, [string] $Message) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notmatch $Message) { throw }
        return
    }
    throw "Expected rejection matching '$Message'."
}

& "$PSScriptRoot/Test-ReleaseVersion.ps1" -Tag 'v1.1.1' -Version '1.1.1' -Prerelease $false
& "$PSScriptRoot/Test-ReleaseVersion.ps1" -Tag 'v1.2.0-preview.1' -Version '1.2.0-preview.1' -Prerelease $true
Assert-Rejected {
    & "$PSScriptRoot/Test-ReleaseVersion.ps1" -Tag 'v1.1.0' -Version '1.1.1' -Prerelease $false
} 'tag does not match'
Assert-Rejected {
    & "$PSScriptRoot/Test-ReleaseVersion.ps1" -Tag 'v1.1.1' -Version '1.1.1' -Prerelease $true
} 'release type does not match'
Assert-Rejected {
    & "$PSScriptRoot/Test-ReleaseVersion.ps1" -Tag 'v1.2.0-preview.1' -Version '1.2.0-preview.1' -Prerelease $false
} 'release type does not match'

# Simulate feed indexing beyond the old 60-attempt limit without network calls or waits.
$state = @{ Checks = 0; Sleeps = 0 }
function Start-Sleep { param([int] $Seconds) $state.Sleeps++ }
& "$PSScriptRoot/Wait-PackageIndexing.ps1" -PackageId 'Example' -IsPublished {
    param($id)
    if ($id -cne 'Example') { throw 'Incorrect package identity.' }
    $state.Checks++
    return $state.Checks -eq 65
}
if ($state.Checks -ne 65 -or $state.Sleeps -ne 64) { throw 'Delayed indexing was not retried correctly.' }
$state.Sleeps = 0
Assert-Rejected {
    & "$PSScriptRoot/Wait-PackageIndexing.ps1" -PackageId 'Example' -Attempts 2 -IsPublished { $false }
} 'not indexed yet'
if ($state.Sleeps -ne 1) { throw 'Indexing retries must be bounded without a final sleep.' }
Assert-Rejected {
    & "$PSScriptRoot/Wait-PackageIndexing.ps1" -PackageId 'Example' -IsPublished { throw 'Source commit mismatch' }
} 'Source commit mismatch'

$root = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts/ci-check-tests/$([guid]::NewGuid().ToString('N'))"
[System.IO.Directory]::CreateDirectory($root) | Out-Null
$path = Join-Path $root 'coverage.cobertura.xml'
$valid = @'
<coverage><packages>
  <package name="XBullet.EasyMapping.Generator" line-rate="0.96" branch-rate="0.85" />
  <package name="XBullet.EasyMapping.Abstractions" line-rate="1" branch-rate="1" />
</packages></coverage>
'@
[System.IO.File]::WriteAllText($path, $valid)
$inDirectory = Join-Path $root 'run/In/machine'
[System.IO.Directory]::CreateDirectory($inDirectory) | Out-Null
[System.IO.File]::WriteAllText((Join-Path $inDirectory 'coverage.cobertura.xml'), $valid)
# Coverage reports use invariant decimals even on developer machines with comma decimals.
$originalCulture = [System.Globalization.CultureInfo]::CurrentCulture
try {
    [System.Globalization.CultureInfo]::CurrentCulture = 'uk-UA'
    & "$PSScriptRoot/Test-Coverage.ps1" -ResultsDirectory $root
}
finally { [System.Globalization.CultureInfo]::CurrentCulture = $originalCulture }
foreach ($invalid in @(
    @{ Xml = $valid.Replace('line-rate="0.96"', 'line-rate="0.94"'); Error = 'below' },
    @{ Xml = $valid.Replace('branch-rate="0.85"', 'branch-rate="0.79"'); Error = 'below' },
    @{ Xml = $valid.Replace('branch-rate="0.85"', 'branch-rate="NaN"'); Error = 'Invalid' },
    @{ Xml = $valid.Replace('line-rate="0.96"', 'line-rate="1.01"'); Error = 'Invalid' },
    @{ Xml = $valid.Replace('XBullet.EasyMapping.Generator', 'Other'); Error = 'Expected one coverage entry' }
)) {
    [System.IO.File]::WriteAllText($path, $invalid.Xml)
    Assert-Rejected { & "$PSScriptRoot/Test-Coverage.ps1" -ResultsDirectory $root } $invalid.Error
}
$empty = Join-Path $root 'empty'
[System.IO.Directory]::CreateDirectory($empty) | Out-Null
Assert-Rejected { & "$PSScriptRoot/Test-Coverage.ps1" -ResultsDirectory $empty } 'Expected one coverage report'
[System.IO.File]::WriteAllText((Join-Path $empty 'coverage.cobertura.xml'), $valid)
Assert-Rejected { & "$PSScriptRoot/Test-Coverage.ps1" -ResultsDirectory $root } 'Expected one coverage report'
Write-Output 'Verified delayed indexing, bounded retries, immediate mismatch rejection, and coverage failure gates.'
