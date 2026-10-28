[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Tag,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,
    [Parameter(Mandatory = $true)]
    [bool] $Prerelease
)

$ErrorActionPreference = 'Stop'
if ($Tag -cne "v$Version") { throw 'The tag does not match the package version.' }
if ($Prerelease -ne $Version.Contains('-')) { throw 'The GitHub release type does not match the package version suffix.' }
