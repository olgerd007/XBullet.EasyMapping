[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageId,
    [Parameter(Mandatory = $true)]
    [scriptblock] $IsPublished,
    [ValidateRange(1, 180)]
    [int] $Attempts = 180,
    [ValidateRange(1, 30)]
    [int] $IntervalSeconds = 5
)

$ErrorActionPreference = 'Stop'
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    if (& $IsPublished $PackageId) { return }
    if ($attempt -lt $Attempts) { Start-Sleep -Seconds $IntervalSeconds }
}
throw "$PackageId was pushed but is not indexed yet. Check NuGet before rerunning this release."
