[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryUrl = 'https://github.com/olgerd007/XBullet.EasyMapping'
$license = [System.IO.File]::ReadAllText((Join-Path $repositoryRoot 'LICENSE')).Replace("`r`n", "`n").TrimEnd()
$packages = @(
    @{ Id = 'XBullet.EasyMapping.Abstractions'; Assembly = 'lib/netstandard2.0/XBullet.EasyMapping.Abstractions.dll' },
    @{ Id = 'XBullet.EasyMapping'; Assembly = 'analyzers/dotnet/cs/XBullet.EasyMapping.Generator.dll' }
)

foreach ($package in $packages) {
    $path = Join-Path $PackageDirectory "$($package.Id).$ExpectedVersion.nupkg"
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing release package: $path" }
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $path).Path)
    try {
        if (@($archive.Entries | Group-Object -Property FullName | Where-Object { $_.Count -gt 1 }).Count) {
            throw "Duplicate archive entries: $path"
        }
        function Read-PackageText([string] $Name) {
            $entry = $archive.GetEntry($Name)
            if ($null -eq $entry) { throw "Missing '$Name' in $($package.Id)." }
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
        }

        [xml] $manifest = Read-PackageText "$($package.Id).nuspec"
        $metadata = $manifest.package.metadata
        if ($metadata.id -cne $package.Id -or $metadata.version -cne $ExpectedVersion) { throw "Incorrect package identity/version: $path" }
        if ($metadata.license.type -cne 'expression' -or $metadata.license.InnerText -cne 'MIT') { throw "Missing MIT license metadata: $path" }
        if ([string]::IsNullOrWhiteSpace($metadata.authors)) { throw "Missing package author: $path" }
        if ($metadata.projectUrl -cne $repositoryUrl -or $metadata.repository.url -cne $repositoryUrl -or $metadata.repository.type -cne 'git') {
            throw "Incorrect repository metadata: $path"
        }
        if ($metadata.repository.commit -notmatch '^[0-9a-f]{40}$') { throw "Missing source commit metadata: $path" }
        if ($metadata.readme -cne 'README.md' -or [string]::IsNullOrWhiteSpace((Read-PackageText 'README.md'))) { throw "Missing package README: $path" }
        if ((Read-PackageText 'LICENSE').Replace("`r`n", "`n").TrimEnd() -cne $license) { throw "Package license differs from repository license: $path" }
        if ($null -eq $archive.GetEntry($package.Assembly)) { throw "Missing package assembly: $path" }

        $unexpectedAssemblies = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase) -and $_.FullName -cne $package.Assembly })
        if ($unexpectedAssemblies.Count) { throw "Unexpected runtime/compiler assemblies: $path" }
        $dependencies = @($manifest.SelectNodes("//*[local-name()='dependency']"))
        if (@($dependencies | Where-Object { $_.id.StartsWith('Microsoft.CodeAnalysis', [StringComparison]::Ordinal) }).Count) {
            throw "Compiler dependencies leaked into the package contract: $path"
        }
        if ($package.Id -ceq 'XBullet.EasyMapping') {
            foreach ($asset in @('lib/netstandard2.0/_._', 'buildTransitive/XBullet.EasyMapping.targets', 'buildTransitive/MapperWriteReceivedApprovals.cs', 'tools/Export-ModelApprovals.ps1')) {
                if ($null -eq $archive.GetEntry($asset)) { throw "Missing generator integration asset '$asset'." }
            }
            $attributes = @($dependencies | Where-Object { $_.id -ceq 'XBullet.EasyMapping.Abstractions' })
            $escapedVersion = [regex]::Escape($ExpectedVersion)
            if ($attributes.Count -ne 1 -or $attributes[0].version -notmatch "^(?:$escapedVersion|\[$escapedVersion\]|\[$escapedVersion,\s*\))$") {
                throw 'The generator must depend on the matching attributes package version.'
            }
            if ((Read-PackageText 'buildTransitive/XBullet.EasyMapping.targets') -notmatch 'MapperWriteReceivedApprovals\.cs') { throw 'The packaged targets do not reference the packaged task.' }
        }
        Write-Output "Verified package identity, metadata, license, README, and assets: $($package.Id) $ExpectedVersion"
    }
    finally { $archive.Dispose() }
}
