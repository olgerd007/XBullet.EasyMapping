[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $PackageSource,
    [string] $PackageCache,
    [ValidateSet('Source', 'Project', 'Custom', 'Existing', 'Linked', 'External')]
    [string[]] $Locations = @('Source', 'Project', 'Custom', 'Existing', 'Linked', 'External')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = Split-Path -Parent $PSScriptRoot
foreach ($location in $Locations) {
    $testRoot = Join-Path $repositoryRoot "artifacts/model-approval-tests/$([guid]::NewGuid().ToString('N'))/Consumer with spaces"
    [System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
    if ($location -eq 'Source') {
        [System.IO.File]::WriteAllText((Join-Path $testRoot 'Directory.Build.props'), @"
<Project>
  <Import Project="$repositoryRoot/Directory.Build.props" />
  <PropertyGroup>
    <BaseOutputPath>Build output/</BaseOutputPath>
    <BaseIntermediateOutputPath>Build intermediate/</BaseIntermediateOutputPath>
  </PropertyGroup>
</Project>
"@)
    }
    $project = Join-Path $testRoot 'Consumer.csproj'
    $sourceDirectory = Join-Path $testRoot 'Mappings with spaces'
    if ($location -eq 'Linked') { $sourceDirectory = Join-Path (Split-Path -Parent $testRoot) 'Linked sources' }
    [System.IO.Directory]::CreateDirectory($sourceDirectory) | Out-Null
    $sourceFile = Join-Path $sourceDirectory 'Models.cs'
    $approvalDirectory = switch ($location) {
        'Project' { $testRoot }
        'Custom' { Join-Path $testRoot 'Review/Approvals' }
        'Linked' { $testRoot }
        'External' { Join-Path (Split-Path -Parent $testRoot) 'Shared approvals' }
        default { $sourceDirectory }
    }
    $locationProperty = switch ($location) {
        'Source' { '<DefaultItemExcludes>$(DefaultItemExcludes);Ignored approvals/**</DefaultItemExcludes><DefaultItemExcludesInProjectFolder>Project ignored/**</DefaultItemExcludesInProjectFolder>' }
        'Project' { '<MapperApprovalDirectory>$(MSBuildProjectDirectory)</MapperApprovalDirectory>' }
        'Custom' { '<MapperApprovalDirectory>Review/Approvals</MapperApprovalDirectory>' }
        'External' { '<MapperApprovalDirectory>../Shared approvals</MapperApprovalDirectory>' }
        default { '' }
    }
    $linkedSource = if ($location -eq 'Linked') { "<Compile Include=`"$sourceFile`" Link=`"Mappings/Models.cs`" />" } else { '' }
    $received = Join-Path $approvalDirectory 'M.mapper.received.txt'
    $approved = Join-Path $approvalDirectory 'M.mapper.approved.txt'
    $generatorRoot = Join-Path $repositoryRoot 'src/XBullet.EasyMapping.Generator'
    $abstractionsRoot = Join-Path $repositoryRoot 'src/XBullet.EasyMapping.Abstractions'
    $references = if ($PackageSource) {
        '<PackageReference Include="XBullet.EasyMapping" />'
    } else {
        @"
    <Reference Include="XBullet.EasyMapping.Abstractions" HintPath="$abstractionsRoot/bin/$Configuration/netstandard2.0/XBullet.EasyMapping.Abstractions.dll" />
    <Analyzer Include="$generatorRoot/bin/$Configuration/netstandard2.0/XBullet.EasyMapping.Generator.dll" />
"@
    }
    $import = if ($PackageSource) { '' } else {
        "<Import Project=`"$generatorRoot/buildTransitive/XBullet.EasyMapping.targets`" />"
    }
    [System.IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
    $locationProperty
  </PropertyGroup>
  <ItemGroup>
    $linkedSource
    $references
  </ItemGroup>
  $import
</Project>
"@)

    $initial = @'
using XBullet.EasyMapping;
[Mapper(RequireModelApproval = true)]
public static partial class M { public static partial Output Map(Input source); }
public record Input(int Id);
public record Output(int Id);
'@
    [System.IO.File]::WriteAllText($sourceFile, $initial)
    $restoreArguments = @('restore', $project, '--verbosity', 'minimal')
    if ($PackageSource) { $restoreArguments += @('--source', $PackageSource) }
    if ($PackageCache) { $restoreArguments += @('--packages', $PackageCache) }
    & dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'Restoring the model-approval test consumer failed.' }

    function Assert-Build([string] $ExpectedError, [string] $ExpectedMessage) {
        $output = @(& dotnet build $project --no-restore --configuration $Configuration --verbosity minimal -m:1 2>&1)
        $status = $LASTEXITCODE
        if ($ExpectedError) {
            if ($status -eq 0 -or -not ($output -match "error ${ExpectedError}:")) {
                $output | Write-Output
                throw "Expected build failure $ExpectedError."
            }
            $unexpected = @($output | Where-Object { "$_" -match '\berror(?:\s+[A-Z]+\d+)?\s*:' -and "$_" -notmatch "\berror ${ExpectedError}:" -and "$_" -notmatch '\berror CS8795:' })
            if ($unexpected.Count -gt 0) { $output | Write-Output; throw 'Unexpected build error.' }
            if ($ExpectedMessage -and -not (($output -join "`n").Contains($ExpectedMessage))) {
                $output | Write-Output
                throw "Expected diagnostic message containing '$ExpectedMessage'."
            }
        } elseif ($status -ne 0) {
            $output | Write-Output
            throw 'Expected a successful consumer build.'
        }
    }

    function Assert-ApprovalItems([string[]] $ExpectedPaths) {
        $itemOutput = @(& dotnet msbuild $project -nologo -getItem:AdditionalFiles)
        if ($LASTEXITCODE -ne 0) { throw 'Reading approval items failed.' }
        $items = (($itemOutput -join "`n") | ConvertFrom-Json).Items.AdditionalFiles
        if (@($items | Where-Object { $_.Identity.EndsWith('.mapper.received.txt', [StringComparison]::Ordinal) }).Count) {
            throw 'Received files must not be included as AdditionalFiles.'
        }
        $actual = @($items | Where-Object { $_.Identity.EndsWith('.mapper.approved.txt', [StringComparison]::Ordinal) } |
            ForEach-Object { [System.IO.Path]::GetFullPath($_.FullPath) } | Sort-Object)
        $expected = @($ExpectedPaths | ForEach-Object { [System.IO.Path]::GetFullPath($_) } | Sort-Object)
        if (($actual -join "`n") -cne ($expected -join "`n")) {
            throw "Unexpected approval items. Expected: $($expected -join ', '); actual: $($actual -join ', ')"
        }
    }

    # A failed ordinary build must export a plain-text candidate without accepting it.
    Assert-Build 'EMAP011'
    if (-not (Test-Path -LiteralPath $received) -or (Test-Path -LiteralPath $approved)) {
        throw 'Missing approvals must produce only a received file.'
    }
    # The optional helper must use the same location rules as the build integration.
    Remove-Item -LiteralPath $received
    $reviewRequired = $false
    try { & "$PSScriptRoot/Export-ModelApprovals.ps1" -Project $project -Configuration $Configuration | Out-Null }
    catch {
        if ($_.Exception.Message -notlike 'Review the received files,*') { throw }
        $reviewRequired = $true
    }
    if (-not $reviewRequired -or -not (Test-Path -LiteralPath $received) -or (Test-Path -LiteralPath $approved)) {
        throw 'The export helper must write a received file in the same location without approving it.'
    }
    $baseline = [System.IO.File]::ReadAllText($received)
    if (-not $baseline.StartsWith("Mapper model approval v1`n")) { throw 'Received content must be plain approval text.' }
    Assert-ApprovalItems @()
    if ($location -eq 'Source') {
        # Output copies must never be discovered as extra or substitute approvals.
        foreach ($folder in @('bin/ignored', 'obj/ignored', 'Build output/ignored', 'Build intermediate/ignored', 'Ignored approvals', 'Project ignored', '.hidden approvals')) {
            $directory = Join-Path $testRoot $folder
            [System.IO.Directory]::CreateDirectory($directory) | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $directory 'M.mapper.approved.txt'), $baseline)
        }
        Assert-ApprovalItems @()
        Assert-Build 'EMAP011' 'is missing'
    }
    Rename-Item -LiteralPath $received -NewName 'M.mapper.approved.txt'
    if ($location -eq 'Existing') {
        # Existing project-local baselines keep their location even with the new source default.
        $approvalDirectory = Join-Path $testRoot 'MappingApprovals'
        [System.IO.Directory]::CreateDirectory($approvalDirectory) | Out-Null
        $newApproved = Join-Path $approvalDirectory 'M.mapper.approved.txt'
        Move-Item -LiteralPath $approved -Destination $newApproved
        $approved = $newApproved
        $received = Join-Path $approvalDirectory 'M.mapper.received.txt'
    }
    Assert-Build
    & "$PSScriptRoot/Export-ModelApprovals.ps1" -Project $project -Configuration $Configuration | Out-Null
    Assert-ApprovalItems @($approved)

    if ($location -eq 'Source') {
        $originalProject = [System.IO.File]::ReadAllText($project)
        $relativeApproval = [System.IO.Path]::GetRelativePath($testRoot, $approved)
        foreach ($explicitPath in @($relativeApproval, $approved)) {
            $manualItem = "<ItemGroup><AdditionalFiles Include=`"$explicitPath`" /></ItemGroup>"
            [System.IO.File]::WriteAllText($project, $originalProject.Replace('<ItemGroup>', "$manualItem`n  <ItemGroup>"))
            Assert-ApprovalItems @($approved)
            Assert-Build
        }

        $disabledProject = $originalProject.Replace('<PropertyGroup>', '<PropertyGroup><EnableDefaultMapperApprovalItems>false</EnableDefaultMapperApprovalItems>')
        [System.IO.File]::WriteAllText($project, $disabledProject)
        Assert-ApprovalItems @()
        Assert-Build 'EMAP011' 'is missing'
        $manualItem = "<ItemGroup><AdditionalFiles Include=`"$relativeApproval`" /></ItemGroup>"
        [System.IO.File]::WriteAllText($project, $disabledProject.Replace('<ItemGroup>', "$manualItem`n  <ItemGroup>"))
        Assert-ApprovalItems @($approved)
        Assert-Build
        [System.IO.File]::WriteAllText($project, $originalProject)

        $duplicateDirectory = Join-Path $testRoot 'Duplicate approval'
        [System.IO.Directory]::CreateDirectory($duplicateDirectory) | Out-Null
        $duplicate = Join-Path $duplicateDirectory 'M.mapper.approved.txt'
        [System.IO.File]::WriteAllText($duplicate, $baseline)
        Assert-ApprovalItems @($approved, $duplicate)
        Assert-Build 'EMAP011' 'duplicate'
        Remove-Item -LiteralPath $duplicate
        Assert-Build
        Write-Output 'Passed discovery: output/intermediate/received exclusions, explicit relative/absolute includes, manual selection, and duplicate rejection.'
    }

    # Matching additions still require review; the approved baseline must remain untouched.
    $changed = $initial.Replace('(int Id)', '(int Id, string Name)')
    [System.IO.File]::WriteAllText($sourceFile, $changed)
    Assert-Build 'EMAP011'
    if ([System.IO.File]::ReadAllText($approved) -cne $baseline) { throw 'Builds must never overwrite approvals.' }
    $changedSnapshot = [System.IO.File]::ReadAllText($received)
    if ($changedSnapshot -ceq $baseline -or -not $changedSnapshot.Contains('property Name')) { throw 'Received file must contain the changed shape.' }
    Assert-Build 'EMAP011'
    if ([System.IO.File]::ReadAllText($received) -cne $changedSnapshot) { throw 'Repeated builds must produce stable received content.' }
    [System.IO.File]::Move($received, $approved, $true)
    Assert-Build

    # A received file left by copying an approval is removed on the next real compilation.
    [System.IO.File]::WriteAllText($received, $changedSnapshot)
    [System.IO.File]::AppendAllText($sourceFile, "`n// Trigger compilation.`n")
    Assert-Build
    if (Test-Path -LiteralPath $received) { throw 'Matching approvals must clear obsolete received files.' }

    # Invalid mapping plans cannot re-export stale candidates from earlier builds.
    $invalid = $initial.Replace('public record Output(int Id);', 'public record Output(int Id, string Name);')
    [System.IO.File]::WriteAllText($sourceFile, $invalid)
    Assert-Build 'EMAP004'
    if (Test-Path -LiteralPath $received) { throw 'Invalid mappings must not export a stale candidate.' }

    # Opting out removes the approval requirement and must not produce received files.
    [System.IO.File]::WriteAllText($sourceFile, $initial.Replace('RequireModelApproval = true', 'RequireModelApproval = false'))
    Assert-Build
    if (Test-Path -LiteralPath $received) { throw 'Approval opt-out must not produce received files.' }
    if ($location -eq 'Linked' -and (Test-Path -LiteralPath (Join-Path $sourceDirectory 'M.mapper.received.txt'))) {
        throw 'Linked sources must not cause writes outside the project.'
    }

    Write-Output "Passed ($location): missing/changed approvals, rename and replace, stable rebuilds, stale-file cleanup, invalid mappings, and opt-out."
}
