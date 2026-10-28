[CmdletBinding()]
param(
    [string] $PackageDirectory,
    [string] $Version,
    [string] $Commit
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    [xml] $props = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
    $Version = [string]$props.Project.PropertyGroup.Version
}
if (-not $Commit) {
    $Commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the package source commit.' }
}
if (-not $PackageDirectory) { $PackageDirectory = Join-Path $repositoryRoot "artifacts/packages/$Version" }

function Assert-Rejected([scriptblock] $Action, [string] $Message) {
    $rejected = $false
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notmatch $Message) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Expected rejection matching '$Message'." }
}

# Exercise release gates without calling GitHub, NuGet, or the dotnet CLI.
function Invoke-RestMethod {
    param([string] $Uri, $Headers)
    if ($Uri -match '/workflows/validate.yml/runs\?') {
        $run = [pscustomobject]@{
            id = 100; head_sha = $Commit; head_branch = 'main'; event = 'push'
            status = 'completed'; conclusion = 'success'; path = '.github/workflows/validate.yml'
        }
        switch ($publishingTestScenario) {
            'wrong-commit' { $run.head_sha = '0' * 40 }
            'wrong-branch' { $run.head_branch = 'v1.1.0' }
            'pull-request' { $run.event = 'pull_request' }
            'wrong-workflow' { $run.path = '.github/workflows/other.yml' }
            'failed-run' { $run.conclusion = 'failure' }
            'running' { $run.status = 'in_progress' }
        }
        return @{ workflow_runs = @($run) }
    }
    if ($Uri -match '/jobs\?') {
        $windows = [pscustomobject]@{ name = 'Validate (windows-latest)'; status = 'completed'; conclusion = 'success' }
        $ubuntu = [pscustomobject]@{ name = 'Validate (ubuntu-latest)'; status = 'completed'; conclusion = 'success' }
        if ($publishingTestScenario -eq 'missing-job') { return @{ jobs = @($windows) } }
        if ($publishingTestScenario -eq 'duplicate-job') { return @{ jobs = @($windows, $ubuntu, $ubuntu) } }
        if ($publishingTestScenario -eq 'failed-job') { $ubuntu.conclusion = 'failure' }
        if ($publishingTestScenario -eq 'skipped-job') { $ubuntu.conclusion = 'skipped' }
        return @{ jobs = @($windows, $ubuntu) }
    }
    if ($Uri -match '/artifacts\?') {
        $artifact = [pscustomobject]@{ id = 200; name = 'release-packages'; expired = $false; workflow_run = @{ head_sha = $Commit } }
        if ($publishingTestScenario -eq 'missing-artifact') { return @{ artifacts = @() } }
        if ($publishingTestScenario -eq 'duplicate-artifact') { return @{ artifacts = @($artifact, $artifact) } }
        if ($publishingTestScenario -eq 'expired-artifact') { $artifact.expired = $true }
        if ($publishingTestScenario -eq 'wrong-artifact-commit') { $artifact.workflow_run.head_sha = '0' * 40 }
        return @{ artifacts = @($artifact) }
    }
    throw "Unexpected GitHub request: $Uri"
}
function Invoke-WebRequest { throw 'Publishing tests must never contact NuGet.' }
function dotnet { throw 'Publishing tests must never execute dotnet or push packages.' }

$publishingTestScenario = 'success'
$artifact = & "$PSScriptRoot/Get-ReleaseArtifact.ps1" -Repository 'owner/repository' -Commit $Commit
if ($artifact.RunId -ne 100 -or $artifact.ArtifactId -ne 200 -or $artifact.Commit -cne $Commit) { throw 'Incorrect artifact selection.' }
$scenarios = @(
    'wrong-commit', 'wrong-branch', 'pull-request', 'wrong-workflow', 'failed-run', 'running',
    'missing-job', 'duplicate-job', 'failed-job', 'skipped-job', 'missing-artifact',
    'duplicate-artifact', 'expired-artifact', 'wrong-artifact-commit'
)
foreach ($publishingTestScenario in $scenarios) {
    Assert-Rejected { & "$PSScriptRoot/Get-ReleaseArtifact.ps1" -Repository 'owner/repository' -Commit $Commit } 'No successful main validation'
}

& "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version $Version -Commit $Commit -WhatIf
$differentCommit = if ($Commit -ceq ('0' * 40)) { '1' * 40 } else { '0' * 40 }
Assert-Rejected {
    & "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version $Version -Commit $differentCommit -WhatIf
} 'Package source commit does not match'
Assert-Rejected {
    & "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version '0.0.0-invalid' -Commit $Commit -WhatIf
} 'Missing release package'

Write-Output "Verified publishing gates: valid artifact, $($scenarios.Count) rejected CI/artifact scenarios, package dry run, commit mismatch, and missing version."
