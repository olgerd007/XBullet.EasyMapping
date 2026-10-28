[CmdletBinding()]
param(
    [ValidateSet('All', 'Benchmark', 'Load')]
    [string] $Mode = 'All',
    [ValidateRange(1, 300)]
    [int] $Seconds = 3,
    [ValidateRange(1, 20)]
    [int] $Repetitions = 3,
    [ValidateRange(1, 256)]
    [int[]] $Workers = @(1, 4),
    [string] $OutputDirectory = 'artifacts/performance'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    dotnet restore XBullet.EasyMapping.slnx --locked-mode --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build benchmarks/MappingComparison.Benchmarks/MappingComparison.Benchmarks.csproj --no-restore -c Release --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    $runner = 'benchmarks/MappingComparison.Benchmarks/bin/Release/net10.0/MappingComparison.Benchmarks.dll'
    if ($Mode -in @('All', 'Benchmark')) {
        dotnet $runner --filter '*PersonMappingBenchmarks*' --job short --warmupCount 3 --iterationCount 8 --iterationTime 250 --artifacts (Join-Path $OutputDirectory 'benchmarkdotnet')
        if ($LASTEXITCODE -ne 0) { throw 'Benchmarks failed.' }
    }
    if ($Mode -in @('All', 'Load')) {
        dotnet $runner load --seconds $Seconds --repetitions $Repetitions --workers ($Workers -join ',') --output (Join-Path $OutputDirectory 'load.json')
        if ($LASTEXITCODE -ne 0) { throw 'Load testing failed.' }
    }
}
finally {
    Pop-Location
}
