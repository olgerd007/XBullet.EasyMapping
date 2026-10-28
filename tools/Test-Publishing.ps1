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
function Invoke-WebRequest {
    param([string] $Uri, [int] $TimeoutSec)
    if ($Uri -notmatch '/v3-flatcontainer/([^/]+)/') { throw "Unexpected NuGet request: $Uri" }
    $id = $Matches[1]
    if ($TimeoutSec -ne 30) { throw 'NuGet reads must have a timeout.' }
    if ($publishingTestScenario -eq 'already-published' -or $publishingTestScenario -eq 'different-published-commit') {
        $sourceCommit = if ($publishingTestScenario -eq 'different-published-commit') { $differentCommit } else { $Commit }
    }
    elseif ($publishingTestScenario -eq 'delayed-publication' -and $publicationState.Pushed.Contains($id)) {
        $publicationState.Reads[$id]++
        if ($publicationState.Reads[$id] -le 64) {
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('Not indexed yet',
                [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound))
        }
        $sourceCommit = $Commit
    }
    elseif ($publishingTestScenario -eq 'delayed-publication') {
        throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('Not published yet',
            [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound))
    }
    else { throw 'Publishing tests must never contact NuGet.' }
    return @{ Content = "<package><metadata><id>$id</id><version>$Version</version><repository commit=`"$sourceCommit`" /></metadata></package>" }
}
function dotnet {
    if ($args.Count -eq 1 -and $args[0] -eq '--version') { return '10.0.403' }
    if ($publishingTestScenario -eq 'delayed-publication' -and $args[0] -eq 'nuget' -and $args[1] -eq 'push') {
        $id = [System.IO.Path]::GetFileName($args[2]) -replace "\.$([regex]::Escape($Version))\.nupkg$", ''
        $id = $id.ToLowerInvariant()
        if (-not $publicationState.Pushed.Add($id)) { throw 'The same package must never be pushed twice.' }
        $publicationState.Reads[$id] = 0
        return
    }
    throw 'Publishing tests must never execute dotnet or push unexpected packages.'
}
function Start-Sleep { param([int] $Seconds) $publicationState.Sleeps++ }

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

$publicationState = @{ Pushed = [System.Collections.Generic.HashSet[string]]::new(); Reads = @{}; Sleeps = 0 }
$originalKey = $env:NUGET_API_KEY
try {
    $env:NUGET_API_KEY = 'offline-test-placeholder'
    $publishingTestScenario = 'already-published'
    & "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version $Version -Commit $Commit
    if ($publicationState.Pushed.Count -ne 0) { throw 'Matching published versions must not be pushed again.' }
    $publishingTestScenario = 'different-published-commit'
    Assert-Rejected {
        & "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version $Version -Commit $Commit
    } 'different source commit'
    $publishingTestScenario = 'delayed-publication'
    & "$PSScriptRoot/Publish-Packages.ps1" -PackageDirectory $PackageDirectory -Version $Version -Commit $Commit
    if ($publicationState.Pushed.Count -ne 2 -or $publicationState.Sleeps -ne 128) {
        throw 'Both packages must be pushed exactly once and tolerate delayed indexing.'
    }
}
finally { $env:NUGET_API_KEY = $originalKey }
Write-Output "Verified publishing gates: $($scenarios.Count) rejected CI/artifact scenarios, package dry run, version/commit rejection, idempotent reruns, and delayed indexing without real network calls or pushes."
