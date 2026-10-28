[CmdletBinding()]
param(
    [switch] $UpdateEvidence,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
# These fixtures deliberately make the compiler return a nonzero exit code.
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtures = @(
    @{ Name = 'Mapperly.UnmappedTarget'; Diagnostic = 'RMG012' },
    @{ Name = 'Facet.InvalidInclude'; Diagnostic = 'FAC004' }
)

Push-Location $repositoryRoot
try {
    foreach ($fixture in $fixtures) {
        $project = "tests/Diagnostics/$($fixture.Name)/$($fixture.Name).csproj"
        $output = @(& dotnet build $project --no-restore --configuration $Configuration --verbosity minimal -m:1 2>&1)
        $buildExitCode = $LASTEXITCODE
        $expectedPattern = "\berror $($fixture.Diagnostic):"
        $diagnostics = @($output | ForEach-Object { "$_" } | Where-Object { $_ -match $expectedPattern })

        if ($buildExitCode -eq 0 -or $diagnostics.Count -eq 0) {
            $output | Write-Output
            throw "Expected build failure with $($fixture.Diagnostic) in $project."
        }

        $unexpectedErrors = @($output | Where-Object {
            "$_" -match '\berror [A-Z]+\d+:' -and "$_" -notmatch $expectedPattern
        })
        if ($unexpectedErrors.Count -gt 0) {
            $unexpectedErrors | Write-Output
            throw "Unexpected diagnostics in $project."
        }

        # Keep evidence independent of checkout location and OS path separators.
        $evidence = ($diagnostics | ForEach-Object {
            $_.Replace($repositoryRoot, '<REPO>').Replace('\', '/')
        } | Sort-Object -Unique) -join "`n"
        $evidencePath = Join-Path $repositoryRoot "docs/evidence/diagnostics/$($fixture.Name).txt"

        if ($UpdateEvidence) {
            [System.IO.Directory]::CreateDirectory((Split-Path -Parent $evidencePath)) | Out-Null
            [System.IO.File]::WriteAllText($evidencePath, "$evidence`n")
        }
        else {
            if (-not (Test-Path -LiteralPath $evidencePath)) {
                throw "Missing diagnostic evidence: $evidencePath. Run with -UpdateEvidence."
            }
            $expected = [System.IO.File]::ReadAllText($evidencePath).Replace("`r`n", "`n").TrimEnd()
            if ($expected -cne $evidence) {
                throw "Diagnostic evidence changed for $($fixture.Name). Inspect and run with -UpdateEvidence."
            }
        }

        Write-Output "Verified expected diagnostic: $($fixture.Diagnostic) ($($fixture.Name))"
    }
}
finally {
    Pop-Location
}

exit 0
