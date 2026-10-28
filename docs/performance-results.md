# Mapper performance results — 2026-10-28

This records the initial baseline before collection optimization. See the
[subsequent optimization results](performance-optimization.md) for current code.

Single-operation tests put EasyMapping ahead of the tested AutoMapper configuration,
but Mapperly with collection copying is faster and allocates less. EasyMapping's generated
collection handling is the clearest optimization opportunity. These are synthetic
developer-machine measurements, not a universal mapper ranking.

## Environment and execution

- Windows 11 (10.0.26300.9711), AMD Ryzen 9 7900X, 12 physical / 24 logical cores.
- .NET SDK 10.0.403, .NET runtime 10.0.14, x64 RyuJIT, concurrent workstation GC.
- EasyMapping 1.1.0 source, AutoMapper 16.2.0, Mapperly 4.3.1, Facet 6.6.15.
- Source library baseline: `660253d5928a2e9a54f33fe67915b54701464391` plus the
  performance harness in this change. No mapper implementations were modified.
- BenchmarkDotNet 0.15.8: 21 cases, one launch, three warmups, eight measured
  iterations at 250 ms, with automatic piloting and overhead measurement.
- The final benchmark and load runs executed sequentially through
  `./tools/Invoke-PerformanceTests.ps1 -OutputDirectory artifacts/performance/final-2026-10-28`.
  No agent-initiated builds or tests ran during these measurements.

See [methodology and reproduction](performance.md) for exact inputs and commands.
The [BenchmarkDotNet report](evidence/performance/2026-10-28/benchmark-report.md)
includes confidence intervals and GC rates; the
[full JSON](evidence/performance/2026-10-28/benchmark-full.json) preserves individual
measurements and environment details.

## Single-operation timing and allocation

One operation creates one DTO. Small has one nested address, 4 tags and 4 previous
addresses; Large has one nested address, 128 tags and 128 previous addresses.
Means below are in nanoseconds; lower time and allocation are better.

| Mapper | Small ns/map | Small B/map | Large ns/map | Large B/map |
| --- | ---: | ---: | ---: | ---: |
| Manual | 77.66 | 464 | 1,579.22 | 6,416 |
| **EasyMapping** | **116.95** | **536** | **1,999.17** | **6,488** |
| MapperlyCopying | 75.97 | 392 | 1,238.84 | 6,344 |
| AutoMapperNullable | 139.80 | 392 | 3,033.70 | 8,568 |
| AutoMapper default | 137.11 | 392 | 3,035.13 | 8,568 |
| Mapperly default (shares tags) | 54.60 | 304 | 1,219.02 | 5,264 |
| Facet default (shares tags) | 79.74 | 472 | 1,436.88 | 5,432 |

The closest comparisons to EasyMapping's null and collection ownership semantics
are Manual, MapperlyCopying and AutoMapperNullable. Default AutoMapper turns null
collections into empty lists; default Mapperly and Facet share the source tags
list. Facet also generates its own destination contract.

For these means, EasyMapping takes approximately 16% less time than
AutoMapperNullable on Small and 34% less on Large. It takes 54% more time than
MapperlyCopying on Small and 61% more on Large, allocating an extra 144 bytes per
mapping in both cases. Compared with the existing manual baseline, EasyMapping
takes 51% more time on Small and 27% more on Large.

The scalar-only NullCollections case is effectively at the handwritten baseline:
EasyMapping 8.52 ns / 56 B, Manual 9.04 ns / 56 B, and default Mapperly 8.53 ns /
56 B. Their confidence intervals overlap; this does not establish a winner.

## Sustained concurrent load

All **84 measured windows completed successfully**, totaling **2,478,390,784
mappings** across seven variants, two graph sizes, one/four workers, and three
repetitions of three seconds per case. Every returned ID was checked under load.
The saved evidence also passes independent batch-count, checksum and minimum
duration checks. Full value and ownership checks ran before measurement.

The tables show **million mappings per second**, as median **[minimum–maximum]**
across the three repetitions; higher is better. Scaling is the ratio of the two
medians, not a prediction for other worker counts. See the
[raw load JSON](evidence/performance/2026-10-28/load.json) for each window's operation
count, elapsed time, allocations and GC counts.

### Small graph

| Mapper | 1 worker M maps/s | 4 workers M maps/s | Scaling |
| --- | ---: | ---: | ---: |
| Manual | 11.692 [10.746–12.377] | 24.178 [21.265–24.336] | 2.07× |
| **EasyMapping** | **10.061 [8.592–10.607]** | **20.975 [19.969–22.577]** | **2.08×** |
| MapperlyCopying | 13.836 [13.818–14.665] | 23.569 [21.940–27.641] | 1.70× |
| AutoMapperNullable | 6.895 [6.677–7.560] | 20.792 [19.464–20.905] | 3.02× |
| AutoMapper default | 6.268 [6.189–6.674] | 19.139 [17.111–21.466] | 3.05× |
| Mapperly default (shares tags) | 21.199 [19.782–21.771] | 37.504 [36.847–53.950] | 1.77× |
| Facet default (shares tags) | 12.293 [9.990–12.579] | 26.049 [24.539–29.485] | 2.12× |

### Large graph

| Mapper | 1 worker M maps/s | 4 workers M maps/s | Scaling |
| --- | ---: | ---: | ---: |
| Manual | 0.768 [0.754–0.771] | 1.614 [1.529–2.072] | 2.10× |
| **EasyMapping** | **0.555 [0.537–0.569]** | **1.469 [1.437–1.690]** | **2.65×** |
| MapperlyCopying | 0.865 [0.669–0.926] | 2.113 [1.721–2.121] | 2.44× |
| AutoMapperNullable | 0.381 [0.369–0.389] | 0.901 [0.900–0.937] | 2.37× |
| AutoMapper default | 0.369 [0.367–0.399] | 0.965 [0.916–0.973] | 2.61× |
| Mapperly default (shares tags) | 1.018 [0.994–1.037] | 2.289 [1.932–2.546] | 2.25× |
| Facet default (shares tags) | 0.812 [0.751–0.827] | 1.990 [1.786–2.071] | 2.45× |

EasyMapping's Large throughput rises from approximately 555 thousand to 1.47
million maps/s at four workers. MapperlyCopying reaches 2.11 million and
AutoMapperNullable 901 thousand maps/s at four workers. For Small at four workers,
EasyMapping and AutoMapperNullable have overlapping ranges and similar medians;
this run does not establish a meaningful throughput advantage there.

Per-operation allocation matches the microbenchmark figures. Across all measured
load windows there were 112,823 Gen0 collections, 3,134 Gen1 collections and zero
Gen2 collections. These aggregate counts span different workloads and operation
totals; use each raw row to assess a specific mapper. Short-lived allocation load
does not establish long-term memory retention behavior.

## Interpretation and limits

The generated EasyMapping sample uses `Select(...).ToList()` for both collection
members, including an identity projection for tags. MapperlyCopying copies tags
directly and fills a capacity-sized previous-address list with a loop. Removing
the identity projection and considering direct, capacity-aware collection mapping
are focused follow-up candidates. The code and allocation delta support this
hypothesis; no profiler attribution or optimization experiment was performed.

An exploratory pass preceded the final run. Allocation counts matched exactly;
absolute timing varied materially between passes. The final run remains a single
process launch per case on an interactive developer machine. Treat broad gaps and
allocation differences as useful evidence, and repeat with more launches on an
idle, controlled host before setting a release performance budget.

These measurements exclude cold start, generator build time, existing-target
updates, serialization, I/O, EF projections, long-duration retention, and production
request latency. The load harness includes common delegate/loop/checksum costs,
so its throughput must not be equated with the inverse microbenchmark mean.

## Harness validation

- Locked solution restore completed successfully.
- Solution Debug build and benchmark Release build passed with zero warnings and
  zero errors.
- `dotnet test tests/MappingComparison.Tests/MappingComparison.Tests.csproj --no-restore --no-build --verbosity minimal`:
  94 passed, including 15 performance-harness cases.
- `dotnet test tests/XBullet.EasyMapping.Generator.Tests/XBullet.EasyMapping.Generator.Tests.csproj --no-restore --no-build --verbosity minimal`:
  137 passed.
- `dotnet format XBullet.EasyMapping.slnx --no-restore --verify-no-changes` exited
  successfully with zero files formatted. Diagnostic verbosity also reported
  generator workspace reference and duplicate analyzer-release-file messages.
- `./tools/Test-Documentation.ps1`: 31 local links verified across 10 Markdown files.
- `git diff --check` passed. Saved evidence contains all 21 benchmark statistics
  and all 84 load windows with valid batch counts, checksums and durations.

Windows SDK registry access and local test-host process communication required
running .NET validation outside the restricted execution sandbox.
