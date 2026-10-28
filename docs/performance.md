# Mapper performance and load testing

The performance host compares the existing sample mappings without changing the
library. It uses [BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet/)
for isolated timing and managed allocations, and a separate sustained concurrent
runner for throughput and GC behavior. This dependency is confined to the benchmark
host and its tests; it is not included in the EasyMapping packages.

See the [optimization comparison](performance-optimization.md) and the
[initial comparison](performance-results.md) for recorded measurements.

## Reproduce

Run from the repository on an otherwise idle machine with the .NET 10 SDK and
PowerShell 7. Expect several minutes of CPU and allocation load.

```powershell
./tools/Invoke-PerformanceTests.ps1

# Run either part separately, or extend the sustained run.
./tools/Invoke-PerformanceTests.ps1 -Mode Benchmark
./tools/Invoke-PerformanceTests.ps1 -Mode Load -Seconds 10 -Repetitions 5 -Workers 1,4,8
```

The script restores locked dependencies, builds the Release host, then runs the
two workloads sequentially. Results go to ignored `artifacts/performance/`:

- `benchmarkdotnet/results/`: Markdown, CSV, HTML and full BenchmarkDotNet JSON.
- `load.json`: every measured repetition, options, runtime, OS, processor count,
  GC mode, timestamp, completed operations, checksum and collection counts.

Direct commands after restoring and building:

```powershell
dotnet benchmarks/MappingComparison.Benchmarks/bin/Release/net10.0/MappingComparison.Benchmarks.dll --filter '*PersonMappingBenchmarks*' --job short --warmupCount 3 --iterationCount 8 --iterationTime 250 --artifacts artifacts/performance/benchmarkdotnet
dotnet benchmarks/MappingComparison.Benchmarks/bin/Release/net10.0/MappingComparison.Benchmarks.dll load --seconds 3 --repetitions 3 --workers 1,4 --output artifacts/performance/load.json
```

## Workloads and fairness

Each operation creates one person DTO. Inputs and mapper configuration are built
outside measurement. AutoMapper is reused and its mappings are compiled before
timing. These measurements exclude startup, generator build time, serialization,
I/O, existing-target updates and EF projections.

| Scenario | Root fields | Nested address | Tags | Previous addresses |
| --- | --- | --- | --- | --- |
| NullCollections | ID and renamed name | null | null | null |
| Small | ID and renamed name | one | 4 | 4 |
| Large | ID and renamed name | one | 128 | 128 |

Every mapper is checked against the handwritten value baseline and its declared
collection policy before measurement. Root-null behavior is also verified.
These checks run outside the timed region.

| Mapper variant | Copies tags | Null collections |
| --- | --- | --- |
| Manual | yes | preserved |
| EasyMapping | yes | preserved |
| EasyMappingBefore (frozen generated reference) | yes | preserved |
| MapperlyCopying (`UseDeepCloning`) | yes | preserved |
| AutoMapperNullable (`AllowNullCollections`) | yes | preserved |
| AutoMapper default | yes | empty lists |
| Mapperly default | **no, shares source list** | preserved |
| Facet default | **no, shares source list** | preserved |

All variants map nested addresses to new objects and create a new previous-address
list. Facet generates its own DTO type. Default Mapperly and Facet do less copying
than EasyMapping, so their raw speed is not an equivalent ownership guarantee.
The handwritten baseline uses its existing LINQ address mapping; it is not a claim
about the fastest possible manual implementation.

## Measurement method

`EasyMappingBefore` preserves the original generated implementation in the
performance host. It provides a same-run reference for the collection optimization;
it is not a separate mapper library. Value and ownership checks apply to it too.

BenchmarkDotNet runs 24 cases in separate processes with one launch, three warmup
iterations and eight measured iterations targeting 250 ms each, plus automatic
piloting and overhead measurement. Returned DTOs are consumed by BenchmarkDotNet.
Its report includes the mean, error, standard deviation, ratios to Manual, managed
bytes allocated per operation and GC collections. Increase launches and iterations
for release decisions; small differences on a developer machine are inconclusive.

The load runner uses 256 different prebuilt inputs per scenario, shared read-only
across workers. It validates all inputs and warms each mapper for one second before
the measured cases. Dedicated workers synchronize before each window, then map
batches of 256 until the deadline. Each result's ID is checked and added to a
checksum. Cancellation is checked between batches; Ctrl+C cancels the run.
Workers report their completed operation count and thread allocation delta.
Elapsed wall time ends at the last worker's completion, including final batch
overshoot. Allocation excludes input preparation and worker creation.

Small and Large run across eight variants at each selected worker count, three
repetitions by default (96 measured windows with one and four workers).
Case order is shuffled with seed 42 each repetition to reduce ordering effects.
Full GC occurs before each window, outside timing. Collection counts inside the
window are process-wide. The load process uses the active OS power plan and
workstation GC; BenchmarkDotNet reports its own power and runtime settings.
The common delegate dispatch, ID check and loop overhead are included in load
throughput, so it should not be treated as the inverse of the microbenchmark mean.

This is bounded, in-process CPU/allocation stress, not an HTTP load test or a
long-duration leak test. Higher concurrency can reduce throughput through GC,
memory bandwidth and scheduling contention. Inspect all repetitions and record
machine details; do not infer application latency or universal rankings from
these numbers. Performance is not enforced as a CI threshold.
