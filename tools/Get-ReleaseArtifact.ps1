[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string] $Repository,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $Commit
)

$ErrorActionPreference = 'Stop'
$headers = @{ 'User-Agent' = 'Release-artifact-verification'; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2026-03-10' }
if ($env:GITHUB_TOKEN) { $headers.Authorization = 'Bearer ' + $env:GITHUB_TOKEN }
$base = "https://api.github.com/repos/$Repository"
$runs = Invoke-RestMethod -Uri "$base/actions/workflows/validate.yml/runs?head_sha=$Commit&branch=main&event=push&status=success&per_page=100" -Headers $headers
$candidates = @($runs.workflow_runs | Where-Object {
    $_.head_sha -ceq $Commit -and $_.head_branch -ceq 'main' -and $_.event -ceq 'push' -and
    $_.status -ceq 'completed' -and $_.conclusion -ceq 'success' -and $_.path -ceq '.github/workflows/validate.yml'
} | Sort-Object id -Descending)
foreach ($run in $candidates) {
    $jobs = Invoke-RestMethod -Uri "$base/actions/runs/$($run.id)/jobs?filter=latest&per_page=100" -Headers $headers
    $valid = $true
    foreach ($name in @('Validate (windows-latest)', 'Validate (ubuntu-latest)')) {
        $matches = @($jobs.jobs | Where-Object { $_.name -ceq $name })
        if ($matches.Count -ne 1 -or $matches[0].status -cne 'completed' -or $matches[0].conclusion -cne 'success') { $valid = $false }
    }
    if (-not $valid) { continue }
    $artifacts = Invoke-RestMethod -Uri "$base/actions/runs/$($run.id)/artifacts?per_page=100" -Headers $headers
    $packages = @($artifacts.artifacts | Where-Object { $_.name -ceq 'release-packages' -and -not $_.expired })
    if ($packages.Count -ne 1) { continue }
    if ($packages[0].workflow_run -and $packages[0].workflow_run.head_sha -cne $Commit) { continue }
    if ([long]$run.id -le 0 -or [long]$packages[0].id -le 0) { throw 'Invalid GitHub run or artifact identity.' }
    return [pscustomobject]@{ RunId = [long]$run.id; ArtifactId = [long]$packages[0].id; Commit = $Commit }
}
throw "No successful main validation with both required jobs and retained release packages exists for $Commit. Rerun that commit's main validation before publishing."
