[CmdletBinding()]
param(
    [switch] $Verify
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$engines = @('Mapperly', 'Facet', 'EasyMapping')

foreach ($engine in $engines) {
    $generatedRoot = Join-Path $repositoryRoot "samples/MappingComparison.$engine/obj/generated"
    # Approval comments have their own reviewed baselines and may be cleared by an incremental build.
    $generatedFiles = @(Get-ChildItem -LiteralPath $generatedRoot -Filter '*.g.cs' -Recurse |
        Where-Object { -not $_.Name.EndsWith('.ModelApproval.g.cs', [StringComparison]::Ordinal) } | Sort-Object FullName)
    if ($generatedFiles.Count -eq 0) {
        throw "No generated mappings for $engine. Build the solution before exporting."
    }

    $evidenceRoot = Join-Path $repositoryRoot "docs/evidence/generated/$engine"
    foreach ($generatedFile in $generatedFiles) {
        $evidencePath = Join-Path $evidenceRoot "$($generatedFile.Name).txt"
        $actual = [System.IO.File]::ReadAllText($generatedFile.FullName).Replace("`r`n", "`n").TrimEnd() + "`n"
        if ($Verify) {
            if (-not (Test-Path -LiteralPath $evidencePath)) {
                throw "Missing generated evidence: $evidencePath. Run without -Verify."
            }
            $expected = [System.IO.File]::ReadAllText($evidencePath).Replace("`r`n", "`n")
            if ($actual -cne $expected) {
                throw "Generated mapping changed: $($generatedFile.Name). Inspect and export again."
            }
        }
        else {
            [System.IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
            [System.IO.File]::WriteAllText($evidencePath, $actual)
        }
    }

    $snapshots = @(Get-ChildItem -LiteralPath $evidenceRoot -Filter '*.g.cs.txt')
    if ($snapshots.Count -ne $generatedFiles.Count) {
        throw "Snapshot count differs for $engine. Review stale evidence files explicitly."
    }

    Write-Output "$(if ($Verify) { 'Verified' } else { 'Exported' }) $($generatedFiles.Count) generated files for $engine."
}
