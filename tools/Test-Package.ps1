[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$versionOutput = @(& dotnet msbuild "$repositoryRoot/src/XBullet.EasyMapping.Generator/XBullet.EasyMapping.Generator.csproj" -nologo -getProperty:Version)
if ($LASTEXITCODE -ne 0) { throw 'Reading the release version failed.' }
$releaseVersion = ($versionOutput -join "`n").Trim()
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'The release version must be a NuGet semantic version.' }
$packageRoot = Join-Path $repositoryRoot "artifacts/packages/$releaseVersion"
$packageCache = Join-Path $repositoryRoot "artifacts/package-smoke-cache/$([guid]::NewGuid().ToString('N'))"

Push-Location $repositoryRoot
try {
    & dotnet pack src/XBullet.EasyMapping.Abstractions/XBullet.EasyMapping.Abstractions.csproj --no-restore --configuration Release --output $packageRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Packing the configuration attributes failed.' }
    & dotnet pack src/XBullet.EasyMapping.Generator/XBullet.EasyMapping.Generator.csproj --no-restore --configuration Release --output $packageRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Packing the generator failed.' }
    & "$PSScriptRoot/Test-PackageContents.ps1" -PackageDirectory $packageRoot -ExpectedVersion $releaseVersion

    $smokeProject = 'tests/PackageSmoke/PackageSmoke.csproj'
    & dotnet restore $smokeProject --source $packageRoot --packages $packageCache --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Restoring the packed consumer failed.' }
    & dotnet run --project $smokeProject --no-restore --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Executing the packed consumer failed.' }
    & "$PSScriptRoot/Test-ModelApprovals.ps1" -PackageSource $packageRoot -PackageCache $packageCache
}
finally {
    Pop-Location
}
