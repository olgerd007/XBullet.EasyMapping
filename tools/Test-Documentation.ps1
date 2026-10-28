[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$files = @(Get-ChildItem -LiteralPath $repositoryRoot -Filter '*.md' -File)
$files += @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs') -Filter '*.md' -File -Recurse)

function Read-MarkdownProse([string] $Path) {
    $fence = $null
    $lines = foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*(`{3,}|~{3,})') {
            if ($null -eq $fence) { $fence = $Matches[1][0] }
            elseif ($Matches[1][0] -eq $fence) { $fence = $null }
            continue
        }
        if ($null -eq $fence) { $line }
    }
    return $lines -join "`n"
}

function Get-HeadingAnchors([string] $Path) {
    $seen = @{}
    foreach ($line in ((Read-MarkdownProse $Path) -split "`n")) {
        if ($line -notmatch '^#{1,6}\s+(.+?)\s*#*\s*$') { continue }
        $anchor = $Matches[1].ToLowerInvariant() -replace '[^\p{L}\p{Nd}_\s-]', '' -replace '\s', '-'
        if ($seen.ContainsKey($anchor)) { $seen[$anchor]++; "$anchor-$($seen[$anchor])" }
        else { $seen[$anchor] = 0; $anchor }
    }
}

$linkCount = 0
foreach ($file in $files) {
    foreach ($match in [regex]::Matches((Read-MarkdownProse $file.FullName), '\[[^\]\r\n]+\]\((?<link>[^)\r\n]+)\)')) {
        $link = $match.Groups['link'].Value.Trim()
        if ($link.StartsWith('<', [StringComparison]::Ordinal)) { $link = ($link -split '>')[0].Substring(1) }
        else { $link = ($link -split '\s+')[0] }
        if ($link -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or $link.StartsWith('//', [StringComparison]::Ordinal)) { continue }
        $parts = $link -split '#', 2
        $relative = [Uri]::UnescapeDataString(($parts[0] -split '\?', 2)[0])
        $target = if ($relative.Length) { Join-Path $file.DirectoryName $relative } else { $file.FullName }
        if (-not (Test-Path -LiteralPath $target)) { throw "Broken local link '$link' in $($file.FullName)." }
        if ($parts.Count -eq 2 -and $parts[1].Length) {
            if ([System.IO.Path]::GetExtension($target) -ine '.md') { throw "Cannot validate a non-Markdown anchor '$link'." }
            $anchor = [Uri]::UnescapeDataString($parts[1])
            if (@(Get-HeadingAnchors $target) -cnotcontains $anchor) { throw "Broken heading link '$link' in $($file.FullName)." }
        }
        $linkCount++
    }
}
Write-Output "Verified $linkCount local documentation links across $($files.Count) Markdown files."
