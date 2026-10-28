# AutoMapper replacement investigation

Research date: 2026-10-28.

Status: initial research snapshot, based on official documentation and selected
source inspection. This research phase did not install packages or execute samples.
The subsequent [DTO comparison milestone](dto-comparison-results.md) now contains
executed results, pinned packages, and generated evidence. EF projections were
deferred at the user's request. Sections below retain the original investigation
scope; documented support alone is not a test result.

The user subsequently chose to own the mapping engine. The [Mapping implementation](easy-mapping.md)
now follows that direction; the adoption recommendation below is historical research.

## Recommendation

Use **Mapperly as the baseline candidate for replacing AutoMapper in existing
projects**. It generates mappings between types we already own, which lets us
preserve existing contracts while changing how mapping code is produced.

Evaluate **Facet separately for generating new DTO families and projections**.
Its type-generation model is valuable when deriving a contract from a source model
is intentional, but that is a different decision from replacing a mapping engine.

For Mapping, first establish migration conventions and a representative
comparison suite. Add a shared package only where repeated consumer requirements
justify it. Build an independent generator if owning the engine is a product goal
or the comparison exposes important gaps. A wrapper around another library does
not by itself provide engine independence.

Both candidates already use source generation. The decision is therefore between
adopting generation, integrating it into a shared library, and owning a generator.

## Repository and scope

The repository currently contains `.gitignore` and `AGENTS.md`, with no solution,
projects, tests, target framework, or existing mappings. Those existing files are
outside this documentation change.

Assumptions for this investigation:

- Existing projects have independently defined source and destination types.
- Their public DTO shapes and mapping behavior should survive migration.
- Compile-time diagnostics, inspectable output, and low runtime overhead matter.
- EF Core query projections are relevant, but actual database providers are unknown.
- AutoMapper API compatibility is desirable only when it preserves useful behavior;
  a drop-in implementation of every AutoMapper feature is not an initial goal.

The installed SDK inventory includes .NET 8, 9, and 10. `dotnet --info` reported
10.0.403 but exited unsuccessfully while querying Windows workload/service
information because Service Control Manager access was denied. This does not
establish that builds are blocked. Consumer framework and compiler requirements
must be collected before selecting package targets.

## Evidence and version boundaries

The Mapperly stable documentation and NuGet page identify **4.3.1**. Its documented
tooling requirements include C# 9+ and Roslyn 4.0+; framework compatibility alone
does not establish compiler compatibility.
Sources: [Mapperly package](https://www.nuget.org/packages/Riok.Mapperly/),
[installation](https://mapperly.riok.app/docs/getting-started/installation/).

Facet's GitHub releases page identifies **v6.6.15**, while the fetched unversioned
NuGet page identifies **6.6.8**. The exact 6.6.15 package page and package-index
endpoint could not be retrieved through the research browser. Treat 6.6.15 as an
observed repository release, not a verified restored package. Facet documentation
on `master` can differ from a released package. Pin available package versions and
archive their generated output in the comparison before making compatibility claims.
Sources: [Facet releases](https://github.com/Tim-Maes/Facet/releases),
[Facet package](https://www.nuget.org/packages/Facet/).

Mapperly's repository declares Apache-2.0; Facet's declares MIT. Preserve upstream
license and notice requirements when distributing or adapting source. This report
does not assess the consuming organizations' licensing obligations.
Sources: [Mapperly repository](https://github.com/riok/mapperly),
[Facet repository](https://github.com/Tim-Maes/Facet).

## Capability comparison

The independent-generator column describes proposed work, not existing Mapping
capabilities. Each row describes a category; it does not assert behavioral parity.

| Concern | Mapperly | Facet | Independent Mapping generator |
| --- | --- | --- | --- |
| Primary input | Existing types and partial mapper methods | Source model and partial facet declarations | Proposed: existing types and partial mapper methods |
| Main output | Mapping method implementations | DTO members, constructors, mappings, projection expressions | Only features we implement and test |
| Existing DTO contracts | Natural fit | Evaluate generated versus handwritten member ownership | Explicit contracts would remain handwritten |
| Renames and nested data | Property configuration, flattening, conversions | Include/exclude, `MapFrom`, nested facets, flattening | Member resolution and diagnostics required |
| Immutable targets | Parameterized constructors supported | Generated records and accessor options | Constructor and required-member rules required |
| Existing object updates | Void mapping methods | Opt-in generated reverse/update methods | Separate update contract required |
| Query projection | Generated `IQueryable<T>` methods | Generated `Expression<Func<TSource,TTarget>>` | Separate expression emission required |
| Custom behavior | Handwritten mapping methods | Mapping configurations and hooks | Explicit converter selection required |
| Cycles | Opt-in reference handler | Depth and reference-detection settings | Defer or reject unsupported graphs |
| Diagnostics | Mapping and configuration diagnostics | Declaration, configuration, and source-change analyzers | Core product work, not an optional extra |
| Runtime dependencies | Basic mappings can exclude runtime assets; reference handling needs them | Inspect selected helpers; some use reflection | Proposed: direct calls, no reflection fallback |

Evidence for the table is detailed below. No relative speed or maturity score is
assigned from vendor benchmark charts, stars, or a simple feature count.

## Mapperly assessment

Mapperly fits a migration where contracts remain explicit. A partial mapper owns
the mapping declarations; handwritten conversions can supply cases generation
cannot express. This avoids putting mapping attributes on domain entities.
Sources: [introduction](https://mapperly.riok.app/docs/intro/),
[custom methods](https://mapperly.riok.app/docs/configuration/user-implemented-methods/).

Illustrative usage, adapted to this investigation; not compiled here:

```csharp
using Riok.Mapperly.Abstractions;

public sealed record ProductReadModel(int Id, string Name);
public sealed record ProductResponse(int Id, string DisplayName);

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ProductMappings
{
    [MapProperty(nameof(ProductReadModel.Name), nameof(ProductResponse.DisplayName))]
    public static partial ProductResponse ToResponse(ProductReadModel source);
}
```

Constructor mappings support record contracts. Mapping into an existing mutable
object uses a separate void method. Neither capability implies that a mapper
should bypass an aggregate's business methods.
Sources: [constructors](https://mapperly.riok.app/docs/configuration/ctor-mappings/),
[updates](https://mapperly.riok.app/docs/configuration/existing-target/).

Unmapped source and target members produce warnings by default, with `Both` as
the default required mapping strategy. Select `Target` for intentional source
subsets, or `Both` when source additions must also trigger review. Elevate selected
diagnostics, such as missing target mappings, to errors in CI; enabling a strategy
alone does not change warning severity.
Sources: [configuration](https://mapperly.riok.app/docs/configuration/mapper/),
[diagnostics](https://mapperly.riok.app/docs/configuration/analyzer-diagnostics/).

Mapperly supports generic and runtime-target dispatch over declared mappings.
It does not discover arbitrary new type pairs at runtime: unknown pairs fail.
This gives a possible migration path for some `Map<TDestination>(object)` calls,
but requires an explicit inventory of permitted pairs.
Source: [generic mappings](https://mapperly.riok.app/docs/configuration/generic-mapping/).

Basic mappings can omit Mapperly runtime assets. Opting into reference handling
requires the runtime handler implementation. Test graph identity separately from
value equality when that feature matters.
Sources: [installation](https://mapperly.riok.app/docs/getting-started/installation/),
[reference handling](https://mapperly.riok.app/docs/configuration/reference-handling/).

The inspected `MapperGenerator` implements `IIncrementalGenerator`, builds mapper
descriptions, reports diagnostics, and emits source. This is a useful architectural
reference, not a reason to copy its entire feature set.
Source: [generator implementation](https://github.com/riok/mapperly/blob/main/src/Riok.Mapperly/MapperGenerator.cs).

## Facet assessment

Facet reduces the work of maintaining multiple views of a model. Its generated
surface includes DTO members and construction/projection code; reverse mapping is
optional. This makes it particularly interesting for new read models, internal
reporting, or intentional DTO generation.
Sources: [overview](https://github.com/Tim-Maes/Facet),
[generated examples](https://github.com/Tim-Maes/Facet/blob/master/docs/07_WhatIsBeingGenerated.md).

Illustrative declaration; not compiled here:

```csharp
using Facet;

[Facet(typeof(ProductReadModel),
    Include = [nameof(ProductReadModel.Id), nameof(ProductReadModel.Name)],
    GenerateToSource = false)]
public partial class ProductSummary;
```

Prefer explicit inclusion for externally visible generated contracts. With an
exclusion list, a newly added source property may become an unintended DTO member.
Source-signature tracking is opt-in and emits FAC022; it calls attention to drift
but does not freeze the schema. API contract snapshots remain necessary.
Sources: [attribute reference](https://github.com/Tim-Maes/Facet/blob/master/docs/03_AttributeReference.md),
[source-signature overview](https://github.com/Tim-Maes/Facet#source-signature-tracking-for-breaking-change-detection).

Facet has compile-time analyzers, including invalid member-name and declaration
checks. The exact rule set must be verified against the chosen package.
Source: [analyzer rules](https://github.com/Tim-Maes/Facet/blob/master/docs/13_AnalyzerRules.md).

Do not interpret Facet's source generation as a guarantee that every ecosystem
API is reflection-free. Its mapping documentation distinguishes explicit-type
overloads from inferred-source helpers using runtime reflection. Its expression
transformation package also documents reflection caching and lazy expression
compilation. Prefer direct generated construction and projections when validating
a reflection-free execution path.
Sources: [custom mapping](https://github.com/Tim-Maes/Facet/blob/master/docs/04_CustomMapping.md),
[expression transformation](https://github.com/Tim-Maes/Facet/blob/master/src/Facet.Mapping.Expressions/README.md).

Facet documents async configuration interfaces with dependency injection. That
supports enrichment scenarios, but Mapping should keep database and HTTP work
in use cases: load or batch the data first, then perform a pure transformation.
Otherwise mapping a collection can hide an external call per item.
Source: [custom mapping](https://github.com/Tim-Maes/Facet/blob/master/docs/04_CustomMapping.md).

Reference-detection and depth limits must not be treated as proof of preserving
shared object identity. Inspect and test forward and reverse behavior separately.
Source: [attribute reference](https://github.com/Tim-Maes/Facet/blob/master/docs/03_AttributeReference.md).

## Query projections need their own evaluation

Mapperly generates queryable projections; its documented limitations include no
reference handling or object factories and different null-setting behavior.
Custom methods must satisfy inlining and expression-tree restrictions.
Source: [Mapperly projections](https://mapperly.riok.app/docs/configuration/queryable-projections/).

Facet generates projection expressions. Its documented navigation access is
translated by EF Core, which is why explicit `Include` calls can be unnecessary.
Source: [Facet extensions](https://github.com/Tim-Maes/Facet/blob/master/docs/05_Extensions.md).

This is not a feature unique to Facet: AutoMapper also documents server-side
projection and nested selections. A replacement must preserve query behavior,
not merely produce equivalent DTOs after materialization.
Source: [AutoMapper projections](https://docs.automapper.io/en/stable/Queryable-Extensions.html).

Our validation must check SQL and execution on the production provider. EF Core
can evaluate untranslatable code in the final projection on the client, while
unsupported expressions elsewhere can throw. Compilation and successful
enumeration alone do not prove full SQL translation.
Source: [EF Core client evaluation](https://learn.microsoft.com/en-us/ef/core/querying/client-eval).

Keep query composition and execution in Infrastructure. Application contracts may
return projected DTOs without exposing `IQueryable` or EF Core types. Apply filters,
stable ordering, and bounds before materialization; pass cancellation to async
query execution. Pure in-memory mapping does not need an artificial async API.

## Combining generators: an architectural constraint

Do not design Mapping as a generator that emits Mapperly declarations and
expects Mapperly to process them during the same compiler run. Similarly, do not
assume Mapperly can inspect DTO members Facet just generated in that compilation.
Normal generators see the input compilation rather than each other's normal
generated output. Mapperly has an issue documenting this limitation.
Sources: [Roslyn generator design](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md),
[Mapperly generated-member issue](https://github.com/riok/mapperly/issues/883).

Practical options are handwritten mapper declarations, a separately compiled
project containing generated DTOs, or one generator owning all required output.
A separate contract assembly referencing domain models may itself be undesirable
for shared API contracts; project separation must also respect dependency direction.

## Mapping design alternatives

| Option | Benefit | Cost or limitation | Proposed decision |
| --- | --- | --- | --- |
| Adopt Mapperly directly | Smallest path to generated mappings and existing contracts | Teams adopt its declarations and diagnostics | Baseline for the comparison |
| Add focused Mapping integration | Shared policies, repeated DI adapters, migration guidance | Upstream generator still runs in each consumer | Add only demonstrated shared behavior |
| Use Facet for selected DTOs | Generates contracts and mappings together | Contract ownership and source coupling change | Optional, intentional use cases |
| Own an incremental generator | Full control of API, diagnostics, release policy | Compiler tooling and mapping semantics become our responsibility | Conditional on product goals or proven gaps |

Avoid a provider-neutral facade that promises every engine's behavior. Start with
typed mapping methods. An `IMapper<TSource,TTarget>` boundary is justified only
where consumers need injection or substitution. A mapping library does not need
Domain, Application, Infrastructure, and API projects of its own.

### If we own the generator

Proposed initial package boundaries:

```text
src/
  XBullet.EasyMapping.Abstractions/  # Small configuration attributes
  XBullet.EasyMapping.Generator/     # Roslyn analyzer/generator assets
tests/
  XBullet.EasyMapping.Generator.Tests/
samples/
  XBullet.EasyMapping.Sample/
```

This is a future layout, not created scaffolding. Keep Roslyn dependencies out of
consumer runtime assemblies. Choose generator target and Roslyn version from the
oldest supported compiler host; a `net10.0` sample does not imply a `net10.0`
generator. Package smoke tests must verify analyzer discovery and dependency loading.

Proposed generation pipeline:

```text
Attributed partial mapper
    -> attribute-based discovery
    -> semantic member and constructor analysis
    -> immutable mapping plan and diagnostics
    -> separate object/projection emitters
    -> ordinary generated C# compiled into the consumer
```

Use `IIncrementalGenerator` and `ForAttributeWithMetadataName`. Extract equatable
models early rather than retaining symbols and syntax trees throughout the
pipeline. Collection fields need structural equality too. Honor Roslyn cancellation,
use deterministic names and ordering, and keep generation free of network I/O.
Source: [Roslyn incremental-generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md).

The first release should support declared pairs, public readable/writable members,
explicit renames and ignores, immutable DTO construction, supported nested pairs,
collections, nullability rules, and explicit pure converters. Missing required
target values, ambiguous constructors/converters, and unsafe conversions should
produce actionable diagnostics. Generate no reflection fallback.

Define semantics before implementing features: same-type reference assignment
versus copying; null collections; enum conversion; numeric overflow; constructor
selection; and whether updates replace or mutate collection members. Unsupported
cycles and ambiguous behavior should fail clearly instead of silently truncating.
Defer automatic reverse mapping, private-member mutation, arbitrary runtime pair
discovery, expression rewriting, and DTO generation until justified.

Treat query projections as a distinct feature with their own supported-operation
set. A shared mapping plan can feed two emitters, but object-only converters must
not silently enter query expressions. Do not advertise projections until provider
integration tests pass.

## Migration strategy

1. Inventory actual AutoMapper versions, profiles, `CreateMap`, `ReverseMap`,
   `ForMember`, conditions, converters/resolvers, `Map` overloads, and `ProjectTo`.
   Include collection plugins, mapping context, inheritance, and runtime type use.
2. Capture current output and error behavior for representative cases. Distinguish
   intended behavior from bugs before making compatibility assertions.
3. Port one mapping family while retaining existing DTOs. Keep mapping definitions
   beside the feature that owns the transformation.
4. Replace simple `Map` calls with typed methods; write reverse directions explicitly.
   Route aggregate updates through domain behavior rather than generated setters.
5. Move I/O-dependent resolvers into explicit use-case enrichment. Register instance
   mappers with lifetimes compatible with their dependencies.
6. Migrate `ProjectTo` independently, inspect SQL, and execute on the real provider.
7. Remove AutoMapper registration and dependencies only after the migrated scope has
   no remaining consumers and its behavior tests pass.

Pay particular attention to collection updates: AutoMapper documents clearing an
existing destination collection before mapping, and mapping null collection
properties to empty collections by default. Those settings can be overridden.
Do not assume a replacement has the same defaults.
Source: [AutoMapper collections](https://docs.automapper.io/en/stable/Lists-and-arrays.html).

DI-dependent object mapping is also distinct from projection: AutoMapper's own
documentation excludes DI value resolvers/converters from query projections.
Source: [AutoMapper DI](https://docs.automapper.io/en/stable/Dependency-injection.html).

## Comparison spike and acceptance criteria

Use the same inputs and expected behavior for handwritten mapping, the installed
AutoMapper baseline, Mapperly, and Facet where its type-generation model applies.
Use separate projects for competing generators to avoid accidental interaction.
Add a custom-generator candidate only if that path is chosen; it does not exist yet.

| Scenario | Required evidence |
| --- | --- |
| Scalars, renames, ignores | Exact output; absent sensitive members |
| Null roots, nested objects, collections | Documented return/throw/default behavior |
| Records, `init`, `required` | Generated consumer compiles and values are correct |
| Missing members and ambiguous conversions | Expected diagnostic IDs, severities, and source locations |
| Nested collections and mutable references | Value, aliasing, and copy behavior assertions |
| Existing target update | Identity, untouched fields, collection behavior, explicit-null handling |
| Inheritance and runtime dispatch | Known derived pairs work; unknown pairs fail clearly |
| Cyclic/shared graphs | Termination plus identity assertions where preservation is required |
| Custom converters and enrichment | Deterministic results; no hidden I/O in generated maps |
| EF projections | Production-provider SQL inspection, null/navigation tests, bounded queries |
| Packaging and framework support | Restore/build sample from the packed NuGet on each supported compiler |
| Trimming and Native AOT, if required | Publish and execute the actual sample; inspect warnings |
| Incremental generation, if owned | Unrelated edits avoid unnecessary regeneration; deterministic snapshots |

Use a relational provider for projection tests; LINQ-to-Objects and EF InMemory
cannot establish SQL translation. Capture command counts and selected columns,
including nested collection queries, rather than asserting one query in all cases.

Benchmark only after behavior is equivalent. Use BenchmarkDotNet in Release with
fixed SDK/package versions and the same graph sizes, null policies, reference
handling, cloning, and converters. Measure allocations and throughput separately
from cold startup, clean build, incremental build, and IDE responsiveness. Do not
mix database/network time into a pure mapping benchmark.

Decision gate: choose the least complex option that passes all must-have behavior,
query, tooling, and packaging checks. Classify each gap as configurable, requiring
explicit handwritten code, or unsupported. Do not hide gaps in an aggregate score.

## Follow-up to this investigation

The [first comparison milestone](dto-comparison-results.md) implements immutable
DTOs, null/collection behavior, existing-target updates, generated output snapshots,
and diagnostic fixtures using synthetic inputs. Real profiles were not available.
EF projections are deferred and are not a prerequisite for the first DTO-focused
implementation. Use the executed results to decide whether Mapping needs
conventions, focused integration code, or an independently maintained generator.

Outstanding inputs are the consuming projects' frameworks and compilers, their
AutoMapper profiles and extensions, database providers, Native AOT requirements,
and whether owning the generator is a primary product requirement. None prevents
this investigation, but each affects the implementation decision.
