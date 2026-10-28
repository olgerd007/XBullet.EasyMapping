# DTO comparison milestone

Executed on 2026-10-28 with .NET SDK 10.0.403 and .NET runtime 10.0.14 on Windows.

This document records the original comparison milestone. A subsequent request
explicitly chose an independently owned generator. See the [Mapping guide](mapping.md)
for that implementation; the expanded suite now has 79 comparison cases and 81
compiler tests. Its collection behavior matches the handwritten baseline here:
preserve null, copy containers, and replace destination containers during updates.
The Mapping sample now opts into both-model coverage and explicitly ignores
the internal source note; this does not alter its mapped output.
The sample and package smoke consumer also require explicit model-shape approval;
their initial baselines are included in their `MappingApprovals/` directories.

## Outcome

The comparison solution builds without warnings and all **68 behavior tests pass**.
The two intentionally invalid projects also produce their expected diagnostics.
These tests record differences rather than asserting that all engines behave alike.

**Mapperly remains the preferred baseline for replacing AutoMapper over existing
DTO contracts.** The copying configuration is a useful starting point when source
list mutation must not affect a mapped DTO. It still replaces the destination list
on updates, whereas the tested AutoMapper configuration preserves that list's
identity. That difference needs an explicit migration decision.

Facet successfully generates an equivalent serialized DTO shape, nested mappings,
and updates in the tested cases. Its contract-generation approach requires a
different destination type and a generated update input. Prefer it where generating
those contracts is intentional, rather than making every existing DTO a facet.

No engine-neutral production facade or independent generator was introduced. The
evidence does not yet demonstrate a need to own a mapping engine.

## Scope and pinned packages

The user narrowed this milestone to DTO mappings and deferred EF projections.
No EF packages, persistence layer, database, query tests, or SQL artifacts are added.
No consumer repository or real AutoMapper profiles were supplied, so the DTOs are
synthetic. They represent mapping concerns, not a complete application or domain.

| Dependency | Version | Purpose |
| --- | --- | --- |
| AutoMapper | 16.2.0 | Configurable runtime baseline; not assumed to match a consumer's installed version |
| Riok.Mapperly | 4.3.1 | Generate methods over existing handwritten DTOs |
| Facet | 6.6.15 | Generate DTOs, object construction, and reverse updates |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test execution |
| xunit | 2.9.3 | Assertions and scenario matrix |
| xunit.runner.visualstudio | 3.1.5 | VSTest discovery and execution |

Package versions were verified through NuGet restore. Direct versions are in
`Directory.Packages.props`; every project has a dependency lock file. No performance
claim or compatibility claim for older frameworks follows from these results.

## Observed behavior

The input is a record with an ID, name, optional address, optional string tags,
optional previous-address list, and an internal-only note. The destination renames
`Name` to `DisplayName`, converts nested address types, and excludes the note.
An independent mutable profile DTO exercises updates without domain invariants.
Record properties use init accessors, but their list members are mutable; these
graphs are not deeply immutable. That is why copying behavior is tested explicitly.

| Behavior | Handwritten | AutoMapper default | AutoMapper with null collections | Mapperly default | Mapperly copying | Facet tested path |
| --- | --- | --- | --- | --- | --- | --- |
| Null source at declared boundary | Null | Null | Null | Null | Null | Null via explicit wrapper |
| Null nested address | Null | Null | Null | Null | Null | Null |
| Null collections | Null | Empty lists | Null | Null | Null | Null |
| Non-null same-type tags | Copy | Copy | Copy | Shared list | Copy | Shared list |
| Nested address list | New converted list | New converted list | New converted list | New converted list | New converted list | New converted list |
| Update name from null | Clears | Clears | Clears | Clears | Clears | Clears |
| Update tags from null | Null | Empty list | Null | Null | Null | Null |
| Update non-null tags | New copied list | Clear/refill existing list | Clear/refill existing list | Assign source list | New copied list | Assign source list |
| Excluded ID/server note during update | Preserved | Preserved | Preserved | Preserved | Preserved | Preserved |

"Default" means default collection/copy behavior with the sample's explicit rename,
ignore, and mapping-completeness configuration. It does not mean no configuration.
"Copying" means Mapperly `UseDeepCloning = true`. List copying here says nothing
about untested cyclic graphs, arbitrary element types, or reference preservation.

Tests assert serialized values and the exact set of public output fields, not just
object equality. Separate mutation and reference assertions expose list aliasing
and destination-list identity. A generated Facet record also round-trips through
JSON. Nulls and empty collections are tested separately.

The mutable update DTO cannot distinguish an omitted JSON field from an explicit
null. These are replacement-style updates, not a presence-aware PATCH contract.
No implicit "ignore null" behavior is promised.

## What migration requires

1. Decide null-collection policy per contract before selecting mapper settings.
   The tested AutoMapper `AllowNullCollections = true` configuration aligns null
   outputs with the manual and generated samples.
2. Enable Mapperly copying where source mutation must not leak into the destination.
   The generated default passes the `Tags` list directly; copying emits a new list.
3. Identify callers retaining destination-list references. Copying alone does not
   reproduce AutoMapper's clear/refill update behavior. Such cases need explicit
   collection-update code or a deliberate contract change with regression tests.
4. Keep handwritten DTOs and mapping declarations near the feature owning them.
   Do not put mapper attributes on domain models or mutate aggregate state through
   generated DTO setters.
5. Keep sensitive fields outside the output contract. Mapperly's sample requires
   target coverage; it intentionally does not require every source field to map.

## Facet-specific integration findings

The record facet declares a renamed `DisplayName` property. Facet supplies a matching
positional constructor parameter in another partial declaration, so the property
initializer must use that parameter: `= DisplayName;`. An unrelated initializer
leaves the generated parameter unread and produces CS8907 under warnings-as-errors.
The sample compiles with the parameter initializer, and a direct-constructor test
checks the resulting value.

The Facet package's runtime attribute dependency must remain available to consumers
of this sample. Marking the entire package `PrivateAssets="all"` prevented
`Facet.Attributes` from reaching the test output, causing System.Text.Json's
attribute inspection to throw `FileNotFoundException`. The sample now keeps
generator/build assets private while allowing runtime dependencies to flow.
Serialization tests exercise that consumer boundary.

Facet's generated constructor expects a non-null source. `FacetMappings.ToDto`
defines a nullable-root wrapper explicitly; null-root results are not attributed
to the native constructor. No reflection-based convenience extensions are used.

For updates, Facet generates `ProfileUpdateFacet` from selected `EditableProfile`
members, then `ApplyToSource` mutates an existing profile. This does not directly
consume the independently owned `ProfileUpdate` record. The test-only comparison
bridge explicitly transfers its two fields; this adaptation cost remains visible.
Projection generation is disabled in every facet declaration for this milestone.

## Compiler diagnostics and generated evidence

The diagnostic projects are deliberately excluded from the successful solution:

| Fixture | Expected failure | Verification |
| --- | --- | --- |
| Mapperly.UnmappedTarget | RMG012: target member has no source | Root `.editorconfig` promotes the diagnostic to an error |
| Facet.InvalidInclude | FAC004: included member does not exist | Facet analyzer rejects the configuration |

`Test-GeneratorDiagnostics.ps1` requires a nonzero build exit, the expected error,
no other coded errors, and an unchanged normalized diagnostic snapshot. It therefore
cannot pass just because a build failed for an unrelated reason. This verifies two
specific rules, not exhaustive analyzer coverage.

Reviewed [generated source](evidence/generated) includes two Mapperly files and
six Facet files. [Diagnostic snapshots](evidence/diagnostics) replace checkout paths
with `<REPO>`. Generated copies use a `.txt` extension to avoid duplicate compilation.
The export script verifies the generated content against those copies. Ordinary
incremental builds may retain old generator files under `obj`; if removing or
renaming mappings, use a fresh checkout/build and explicitly review stale evidence.

## Validation performed

Successful checks:

```text
dotnet restore XBullet.EasyMapping.slnx --source https://api.nuget.org/v3/index.json
dotnet restore tests/Diagnostics/Mapperly.UnmappedTarget/Mapperly.UnmappedTarget.csproj --source https://api.nuget.org/v3/index.json
dotnet restore tests/Diagnostics/Facet.InvalidInclude/Facet.InvalidInclude.csproj --source https://api.nuget.org/v3/index.json
dotnet build XBullet.EasyMapping.slnx --no-restore --verbosity minimal -m:1
dotnet test tests/MappingComparison.Tests/MappingComparison.Tests.csproj --no-restore --no-build --verbosity minimal
dotnet format XBullet.EasyMapping.slnx --no-restore --verify-no-changes --verbosity minimal
./tools/Test-GeneratorDiagnostics.ps1 -UpdateEvidence
./tools/Export-GeneratedMappings.ps1
dotnet restore XBullet.EasyMapping.slnx --locked-mode
dotnet restore tests/Diagnostics/Mapperly.UnmappedTarget/Mapperly.UnmappedTarget.csproj --locked-mode
dotnet restore tests/Diagnostics/Facet.InvalidInclude/Facet.InvalidInclude.csproj --locked-mode
./tools/Test-GeneratorDiagnostics.ps1
./tools/Export-GeneratedMappings.ps1 -Verify
```

The test host needed execution outside the restricted tool sandbox to start and
communicate successfully. An initial parallel build returned a failure without
compiler diagnostics; the serial build above completed with zero warnings/errors.
These are local environment observations, not attributed mapping-library failures.

The repository's CI workflow repeats the successful solution checks and verifies
diagnostic/generated snapshots with locked restore. Adding the workflow does not
mean a remote CI run has executed.

## Limits and next decision

This milestone does not test consumer profiles, previous AutoMapper versions,
presence-aware patches, inheritance, enum conversions, arbitrary custom converters,
cycles, DI-dependent enrichment, trimming, Native AOT, performance, or EF projections.
No packages are produced for distribution.

The next implementation decision is whether a small Mapperly-based convention is
sufficient or a concrete missing capability justifies Mapping integration.
Before migrating a consumer, import its DTO/profile family and preserve intended
null, copying, and update semantics in tests. An independently owned generator
should follow an explicit engine-ownership requirement or demonstrated gaps.
