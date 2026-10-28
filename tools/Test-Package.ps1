[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $repositoryRoot 'artifacts/packages'
$packageCache = Join-Path $repositoryRoot "artifacts/package-smoke-cache/$([guid]::NewGuid().ToString('N'))"

Push-Location $repositoryRoot
try {
    & dotnet pack src/XBullet.EasyMapping.Abstractions/XBullet.EasyMapping.Abstractions.csproj --no-restore --configuration Release --output $packageRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Packing the configuration attributes failed.' }
    & dotnet pack src/XBullet.EasyMapping.Generator/XBullet.EasyMapping.Generator.csproj --no-restore --configuration Release --output $packageRoot --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Packing the generator failed.' }

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
