# Collection mapping optimization — 2026-10-28

The optimized generator reduces nested-person mapping time by approximately 41%
for Small and 35% for Large in the same-run comparison, saving 144 bytes per
mapping. It now matches MapperlyCopying's allocations and has comparable timing.

The [initial comparison](performance-results.md) identified two LINQ projection
iterators per person mapping. The generator removes identity projections and fills
converted lists directly when the source has a known count.

## Implementation and compatibility

- Identical element conversions use direct `ToList` / `ToArray` materialization.
  Containers are still copied; element converters retain precedence.
- Converted list targets from arrays, `List<T>`, `IList<T>`, `ICollection<T>`,
  `IReadOnlyList<T>` and `IReadOnlyCollection<T>` use a capacity-sized list and a
  generated `foreach` loop.
- Small local functions take the collection as an explicit parameter. This keeps
  each source getter evaluated once, at its original constructor/assignment
  position. Static mappers use static local functions; instance converters remain
  supported. Names avoid collisions with parameters and mapper members.
- Arbitrary enumerable types and converted array targets keep the existing LINQ
  path. No counting pass, reflection, runtime mapping service or package dependency
  was added to the library.
- Null collections, null elements, numeric conversions, nested/jagged collection
  behavior, deep-copy boundaries and replacement-style updates retain their rules.
  Public model approvals are unchanged.

The generator changes are in `ConversionBuilder.cs` and `MappingPlanner.cs`.
Sixteen new compiler/runtime regression cases cover counted contracts, independent
identity copies, nullable getters, instance and same-type converters, nested list
helpers, single-use enumerables, disposal and generated-name collisions.

## Comparison method

`PreOptimizationMappings` is a frozen equivalent of the generated person mapping
from the initial baseline. It retains both original `Select(...).ToList()` calls
and appears as `EasyMappingBefore` in timing and load reports. It lives only in
the performance host. All variants, including this reference, pass value and
ownership validation before measurement.

The same-run reference reduces dependence on timing differences between separate
developer-machine sessions. It is compiled in the benchmark assembly; optimized
EasyMapping is generated in the sample assembly. This is a source-equivalent
reference, not execution of the old NuGet binary. Allocation differences and broad
timing gaps are more reliable than small ordering differences.

Reproduction uses the same .NET 10 / Ryzen 9 7900X host and measurement settings as
the original baseline: 24 isolated BenchmarkDotNet cases, one launch, three warmups,
eight measured iterations at 250 ms, then 96 three-second load windows covering
eight variants, two graph sizes, one/four workers and three repetitions.

```powershell
./tools/Invoke-PerformanceTests.ps1 -OutputDirectory artifacts/performance/optimized-2026-10-28
```

Inputs and configuration stay outside measurement. See [full methodology](performance.md)
for collection ownership differences, shared load-runner overhead, GC policy and
limitations. Runs measure DTO creation and allocations, not cold start, build time,
updates, EF projections, HTTP traffic or long-term retention.

## Single-operation results

Lower time and allocation are better. Each operation creates one person DTO.
Small has one nested address, 4 tags and 4 previous addresses; Large has one nested
address, 128 tags and 128 previous addresses. The
[BenchmarkDotNet report](evidence/performance/2026-10-28-optimized/benchmark-report.md)
includes error and standard deviation; the
[full JSON](evidence/performance/2026-10-28-optimized/benchmark-full.json) retains
the individual measurements.

| Variant | Small ns/map | Small B/map | Large ns/map | Large B/map |
| --- | ---: | ---: | ---: | ---: |
| EasyMappingBefore | 86.80 | 536 | 1,920.01 | 6,488 |
| **EasyMapping optimized** | **51.20** | **392** | **1,238.46** | **6,344** |
| MapperlyCopying | 57.40 | 392 | 1,225.60 | 6,344 |
| Manual | 79.71 | 464 | 1,289.30 | 6,416 |
| AutoMapperNullable | 133.01 | 392 | 2,922.92 | 8,568 |
| AutoMapper default | 135.91 | 392 | 2,581.31 | 8,568 |
| Mapperly default (shares tags) | 38.75 | 304 | 1,084.38 | 5,264 |
| Facet default (shares tags) | 60.98 | 472 | 1,296.18 | 5,432 |

The old/new mean ratios imply approximately **1.70×** speed on Small and **1.55×**
on Large. Small allocation falls 26.9%; Large allocation falls 2.2%. Both lose the
same two 72-byte projection iterators. This is a fixed allocation reduction, not
a per-element saving. On Large, the timing confidence intervals for optimized
EasyMapping and MapperlyCopying overlap; the run does not establish a winner
between them. Mapperly/Facet defaults still provide different ownership semantics.

## Sustained concurrent load

All 96 windows completed successfully, totaling **2,896,571,392 mappings**. Saved
operation counts, batch sizes, checksums and minimum three-second durations were
independently checked. The [raw load JSON](evidence/performance/2026-10-28-optimized/load.json)
includes all eight variants and each window's allocations and collection counts.

Values below are **million maps/s**, shown as median **[minimum–maximum]** across
three repetitions. Higher is better. The last column divides the two medians; it
does not imply every repetition improved.

| Graph / workers | Frozen before | Optimized | Median ratio |
| --- | ---: | ---: | ---: |
| Small / 1 | 9.715 [7.479–10.035] | 14.556 [10.671–15.050] | 1.50× |
| Small / 4 | 24.078 [22.796–25.150] | 21.135 [20.017–28.450] | 0.88× |
| Large / 1 | 0.604 [0.492–0.637] | 0.940 [0.719–0.959] | 1.56× |
| Large / 4 | 1.397 [1.245–1.584] | 2.072 [1.592–2.131] | 1.48× |

Large-graph median throughput improves approximately 56% at one worker and 48% at
four workers. Small improves approximately 50% at one worker. Small/four-worker
throughput has a lower optimized median and overlapping ranges; this run does
**not** demonstrate an improvement there. Reduced allocation does not guarantee
better throughput once GC, scheduling and memory contention dominate.

For context, four-worker medians for all tested implementations:

| Variant | Small M maps/s | Large M maps/s |
| --- | ---: | ---: |
| EasyMappingBefore | 24.078 | 1.397 |
| **EasyMapping optimized** | **21.135** | **2.072** |
| MapperlyCopying | 22.536 | 2.023 |
| Manual | 23.464 | 1.869 |
| AutoMapperNullable | 20.854 | 1.019 |
| AutoMapper default | 19.099 | 1.023 |
| Mapperly default (shares tags) | 48.299 | 2.541 |
| Facet default (shares tags) | 32.136 | 2.078 |

Allocations match the microbenchmark counts. Across the full load run, the process
recorded 132,200 Gen0 collections, 3,584 Gen1 collections and zero Gen2 collections.
Totals span different workloads and operation counts; compare the per-operation
allocation or individual raw windows rather than aggregate GC counts.

## Scalar-only follow-up

The main pass measured NullCollections at 11.34 ns optimized versus 8.72 ns for the
frozen reference, both at 56 bytes. Because this path does not materialize a
collection, a separate **three-launch** run repeated just these two cases, with
the same three warmups and eight measured iterations at 250 ms per launch.

| Variant | Mean | Error (99.9% CI half-width) | Allocation |
| --- | ---: | ---: | ---: |
| Optimized | 8.940 ns | 0.434 ns | 56 B |
| Frozen before | 8.685 ns | 0.991 ns | 56 B |

The confidence intervals overlap, and the follow-up does not reproduce a clear
scalar regression. It also does not prove an improvement. The
[follow-up report](evidence/performance/2026-10-28-optimized/scalar-report.md) and
[full measurements](evidence/performance/2026-10-28-optimized/scalar-full.json)
preserve the result. Reproduce it after the main suite completes:

```powershell
dotnet benchmarks/MappingComparison.Benchmarks/bin/Release/net10.0/MappingComparison.Benchmarks.dll --filter '*EasyMapping*NullCollections*' --job short --launchCount 3 --warmupCount 3 --iterationCount 8 --iterationTime 250 --artifacts artifacts/performance/scalar-followup-2026-10-28
```

## Validation and limits

Locked restore, Debug solution/Release host builds, the comparison/harness suite,
generator/compiler suite, formatting, documentation links, generated-code evidence
and unchanged model approvals are checked alongside this change. Packaged generator
consumption and all approval-workflow scenarios also pass. Benchmark discovery-only
commands now retain a successful exit status; failed benchmark reports and critical
validation failures still produce a failure exit code.

Final checks: **94 comparison/harness tests and 153 generator tests passed** (247
total). `dotnet format --no-restore --verify-no-changes` exited successfully with
zero of 112 files requiring formatting. Documentation validation checked 41 local
links across 13 Markdown files; generated evidence and `git diff --check` passed.
Saved evidence contains all 24 main benchmark results, both scalar follow-up
results and all 96 load windows, with three repetitions for every load case.

The generator changes add no packages, public API changes, model-approval changes
or new runtime services. These are bounded synthetic runs on an interactive machine.
Most timing cases use one launch; small gaps and noisy concurrent windows should
not be treated as universal rankings. The Small/four-worker case did not show a
throughput gain. Converted array targets and arbitrary transformed enumerable
types remain candidates for separate measurements before further optimization.
