[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Project,
    [string] $Configuration = 'Debug',
    [switch] $Approve
)

$ErrorActionPreference = 'Stop'
# A first build deliberately returns EMAP011 until a model shape has been approved.
$PSNativeCommandUseErrorActionPreference = $false
$projectFile = (Resolve-Path -LiteralPath $Project).Path
if ([System.IO.Path]::GetExtension($projectFile) -ne '.csproj') { throw 'Project must identify a .csproj file.' }
$projectRoot = Split-Path -Parent $projectFile
$settingsOutput = @(& dotnet msbuild $projectFile -nologo "-p:Configuration=$Configuration" `
    -getProperty:MapperApprovalDirectory,BaseIntermediateOutputPath -getItem:AdditionalFiles)
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the approval directory and AdditionalFiles from the project.' }
$settings = ($settingsOutput -join "`n") | ConvertFrom-Json
$configuredDirectory = $settings.Properties.MapperApprovalDirectory

function Resolve-ProjectPath([string] $Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
}

$intermediateRoot = Resolve-ProjectPath $settings.Properties.BaseIntermediateOutputPath
$receivedRoot = Join-Path $intermediateRoot "model-approvals/$([guid]::NewGuid().ToString('N'))"

function Test-ProjectPath([string] $Path) {
    $prefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    return $Path.StartsWith($prefix, $comparison)
}

function Get-ApprovalDirectory([string] $FileName, [string[]] $Lines) {
    if (-not [string]::IsNullOrWhiteSpace($configuredDirectory)) { return Resolve-ProjectPath $configuredDirectory }
    $approvals = @($settings.Items.AdditionalFiles | Where-Object { [System.IO.Path]::GetFileName($_.Identity) -ceq $FileName })
    if ($approvals.Count -eq 1) {
        $existingPath = Resolve-ProjectPath $approvals[0].Identity
        if (Test-ProjectPath $existingPath) { return Split-Path -Parent $existingPath }
    }
    $marker = '// MapperSourcePath: '
    $metadata = @($Lines | Where-Object { $_.StartsWith($marker, [StringComparison]::Ordinal) })
    if ($metadata.Count -eq 1) {
        $sourcePath = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($metadata[0].Substring($marker.Length)))
        if (-not [string]::IsNullOrWhiteSpace($sourcePath)) {
            $resolved = Resolve-ProjectPath $sourcePath
            if (Test-ProjectPath $resolved) { return Split-Path -Parent $resolved }
        }
    }
    return $projectRoot
}

# Only the selected project emits candidates; references must already be built.
$buildOutput = @(& dotnet build $projectFile --no-restore --configuration $Configuration --verbosity minimal -m:1 `
    -t:Rebuild -p:BuildProjectReferences=false -p:EmitCompilerGeneratedFiles=true `
    "-p:CompilerGeneratedFilesOutputPath=$receivedRoot" 2>&1)
$buildStatus = $LASTEXITCODE
$unexpected = @($buildOutput | Where-Object { "$_" -match '\berror(?:\s+[A-Z]+\d+)?\s*:' -and "$_" -notmatch '\berror EMAP011:' })
if ($unexpected.Count -gt 0 -or ($buildStatus -ne 0 -and -not ($buildOutput -match '\berror EMAP011:'))) {
    $buildOutput | Write-Output
    throw 'Cannot export approvals while other build errors exist. Restore and build project dependencies first.'
}
if ($buildStatus -ne 0) {
    $buildOutput | Where-Object { "$_" -match '\berror EMAP011:' } | Sort-Object -Unique | Write-Output
}

if (-not (Test-Path -LiteralPath $receivedRoot)) { throw 'No approval candidates found. Enable RequireModelApproval on a mapper with generated methods.' }
$candidates = @(Get-ChildItem -LiteralPath $receivedRoot -Recurse -Filter '*.ModelApproval.g.cs' -File)
if ($candidates.Count -eq 0) { throw 'No approval candidates found. Enable RequireModelApproval on a mapper with generated methods.' }
$changed = $false
foreach ($candidate in $candidates) {
    $lines = [System.IO.File]::ReadAllLines($candidate.FullName)
    $start = [Array]::IndexOf($lines, '// <MapperModelApproval>')
    $end = [Array]::IndexOf($lines, '// </MapperModelApproval>')
    if ($start -lt 0 -or $end -le $start + 1) { throw "Invalid generated approval candidate: $($candidate.FullName)" }
    $content = ($lines[($start + 1)..($end - 1)] | ForEach-Object {
        if (-not $_.StartsWith('// ', [StringComparison]::Ordinal)) { throw 'Invalid approval comment prefix.' }
        $_.Substring(3)
    }) -join "`n"
    $content += "`n"
    $fileName = $candidate.Name.Substring(0, $candidate.Name.Length - '.ModelApproval.g.cs'.Length) + '.mapper.approved.txt'
    $approvedRoot = Get-ApprovalDirectory $fileName $lines
    $approvedPath = Join-Path $approvedRoot $fileName
    $receivedPath = Join-Path $approvedRoot ($fileName.Replace('.approved.txt', '.received.txt'))
    $existing = if (Test-Path -LiteralPath $approvedPath) {
        [System.IO.File]::ReadAllText($approvedPath).Replace("`r`n", "`n").TrimEnd("`n") + "`n"
    } else { $null }
    if ($existing -ceq $content) {
        if (Test-Path -LiteralPath $receivedPath) { Remove-Item -LiteralPath $receivedPath }
        Write-Output "Verified model approval: $fileName"
        continue
    }

    $changed = $true
    [System.IO.Directory]::CreateDirectory($approvedRoot) | Out-Null
    [System.IO.File]::WriteAllText($receivedPath, $content)
    Write-Output "Candidate model shape: $receivedPath"
    if ($existing -ne $null) {
        & git --no-pager diff --no-index -- $approvedPath $receivedPath
        if ($LASTEXITCODE -gt 1) { throw 'Unable to show the model approval diff.' }
    }
    else { Write-Output $content }

    if ($Approve) {
        [System.IO.Directory]::CreateDirectory($approvedRoot) | Out-Null
        [System.IO.File]::WriteAllText($approvedPath, $content)
        Remove-Item -LiteralPath $receivedPath
        Write-Output "Approved model shape: $approvedPath"
    }
}

if ($changed -and -not $Approve) { throw 'Review the received files, replace the matching approved files by renaming received to approved, then rebuild. Alternatively rerun with -Approve after review.' }
if ($buildStatus -ne 0 -and -not $Approve) { throw 'Approval validation failed. Check the compiler diagnostics, AdditionalFiles inclusion, and duplicate snapshots.' }
if ($Approve) {
    & dotnet build $projectFile --no-restore --configuration $Configuration --verbosity minimal -m:1 -p:BuildProjectReferences=false
    if ($LASTEXITCODE -ne 0) { throw 'Snapshots were written but the build still fails. Check AdditionalFiles inclusion and duplicate approvals.' }
}
exit 0
