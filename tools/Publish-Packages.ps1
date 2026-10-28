[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $Commit
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
& "$PSScriptRoot/Test-PackageContents.ps1" -PackageDirectory $PackageDirectory -ExpectedVersion $Version -ExpectedCommit $Commit

function Test-PublishedVersion([string] $Id) {
    $lowerId = $Id.ToLowerInvariant()
    $lowerVersion = $Version.ToLowerInvariant()
    try { $content = Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$lowerId/$lowerVersion/$lowerId.nuspec" -TimeoutSec 30 }
    catch {
        if ($null -ne $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $false }
        throw
    }
    $text = if ($content.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($content.Content) } else { [string]$content.Content }
    [xml] $manifest = $text
    $metadata = $manifest.package.metadata
    if ($metadata.id -ine $Id -or $metadata.version -cne $Version -or $metadata.repository.commit -cne $Commit) {
        throw "NuGet already contains $Id $Version from a different source commit; it cannot be overwritten."
    }
    return $true
}

foreach ($id in @('XBullet.EasyMapping.Abstractions', 'XBullet.EasyMapping')) {
    $path = Join-Path $PackageDirectory "$id.$Version.nupkg"
    if (-not $PSCmdlet.ShouldProcess("$id $Version on NuGet.org", 'Publish verified package')) { continue }
    if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) { throw 'The temporary NuGet API key is missing.' }
    $sdkOutput = @(& dotnet --version)
    if ($LASTEXITCODE -ne 0 -or [version](($sdkOutput -join '').Split('-')[0]) -lt [version]'10.0.300') {
        throw 'Publishing requires .NET SDK 10.0.300 or newer for NUGET_API_KEY environment support.'
    }
    if (-not (Test-PublishedVersion $id)) {
        & dotnet nuget push $path --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) { throw "Publishing $id failed." }
    } else { Write-Output "Already published from this release commit: $id $Version" }
    & "$PSScriptRoot/Wait-PackageIndexing.ps1" -PackageId $id -IsPublished { param($packageId) Test-PublishedVersion $packageId }
    Write-Output "Verified published version: $id $Version"
}
