[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'
# VSTest can retain another copy under <run>/In/<machine>; consume only published attachments.
$reports = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter 'coverage.cobertura.xml' -File -Recurse -Depth 1)
if ($reports.Count -ne 1) { throw "Expected one coverage report; found $($reports.Count). Use a clean results directory." }
[xml] $coverage = Get-Content -LiteralPath $reports[0].FullName -Raw
$thresholds = @(
    @{ Name = 'XBullet.EasyMapping.Generator'; Line = 95; Branch = 80 },
    @{ Name = 'XBullet.EasyMapping.Abstractions'; Line = 100; Branch = 100 }
)
foreach ($threshold in $thresholds) {
    $packages = @($coverage.coverage.packages.package | Where-Object { $_.name -ceq $threshold.Name })
    if ($packages.Count -ne 1) { throw "Expected one coverage entry for $($threshold.Name)." }
    $rates = @{}
    foreach ($kind in @('line', 'branch')) {
        $rate = 0.0
        if (-not [double]::TryParse([string]$packages[0].GetAttribute("$kind-rate"),
                [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$rate) -or
            -not [double]::IsFinite($rate) -or $rate -lt 0 -or $rate -gt 1) {
            throw "Invalid $kind coverage for $($threshold.Name)."
        }
        $rates[$kind] = $rate * 100
    }
    Write-Output ("{0}: line {1:F2}% (minimum {2}%), branch {3:F2}% (minimum {4}%)." -f
        $threshold.Name, $rates.line, $threshold.Line, $rates.branch, $threshold.Branch)
    if ($rates.line -lt $threshold.Line -or $rates.branch -lt $threshold.Branch) {
        throw "Coverage fell below the required thresholds for $($threshold.Name)."
    }
}
