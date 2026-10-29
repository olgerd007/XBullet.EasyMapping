# Product readiness review — 2026-10-29

Reviewed commit: `42c149e` (version 1.1.1).

**Follow-up:** the 1.1.2 patch addresses all four maintenance issues below,
with 27 added regression cases, including compilation of the published quickstart
and approval refreshes for all eight C# enum underlying types.
All 280 tests pass. Enum-containing approvals require one reviewed refresh; other
baselines and the v1 header remain unchanged. The findings below describe the
reviewed 1.1.1 source; broader support and adoption work remains open.

EasyMapping has a substantial foundation for a focused DTO mapping product:
an owned incremental generator, explicit mapping semantics, strict coverage,
model approvals, tested package consumption, cross-platform CI, API compatibility
checks, and controlled publication. The next milestone should make that existing
promise dependable and easy to adopt.

The review found three reproducible correctness/diagnostic gaps and stale
customer-facing documentation. Wider compiler/framework support, real consumer
validation, and editor/build performance remain unproven. Feature parity with
other mapping libraries is a separate product decision.

## Next maintenance release

### 1. Qualify converter calls to prevent parameter shadowing

**Confirmed defect.** In [ConversionBuilder.Call](../src/XBullet.EasyMapping.Generator/ConversionBuilder.cs),
the emitted call uses an unqualified method name. This valid declaration produces
generated code with **CS0149**, although EasyMapping reports no diagnostic:

```csharp
public record Input(int Id);
public record Output(string Id);

[Mapper]
public static partial class M
{
    public static partial Output Map(Input Convert);

    private static string Convert(int value) => value.ToString();
}
```

Generated constructor argument: `Id: @Convert(@Convert.@Id)`.

**Acceptance:** qualify static and instance converter calls using their appropriate
receiver. Add compiler/runtime regressions for converter names matching source or
update-target parameters, including calls inside collection conversions. Preserve
existing overload and nullability selection rules.

### 2. Reject file-local mapper declarations explicitly

**Confirmed defect.** The mapper validation in
[MappingPlanner.Build](../src/XBullet.EasyMapping.Generator/MappingPlanner.cs)
accepts `file static partial class M`. A generated file cannot complete a
file-local type declared in another file. The resulting compilation reports
**CS0759** and **CS8795**, without an EasyMapping diagnostic.

**Acceptance:** reject file-local mappers with source-located **EMAP001**, emit no
implementation for them, document the restriction, and add a diagnostic regression.
Supporting file-local mappers is unnecessary for the present product scope.

### 3. Include enum underlying types in model approvals

**Confirmed defect.** [ModelShapeBuilder](../src/XBullet.EasyMapping.Generator/ModelShapeBuilder.cs)
records enum names and constants but omits `EnumUnderlyingType`. Starting with
approved models containing `enum Status : int { Ready = 1 }`, changing the enum to
`: long` leaves the approval unchanged. Compilation succeeds without **EMAP011**.
This weakens the advertised structural-change review gate.

**Acceptance:** snapshot the underlying type and test changes with identical enum
values, including enums reachable through nested models. Explain the resulting
baseline refresh in release notes and decide explicitly whether the approval format
needs a new version. Existing approved enum models may require review once.

### 4. Make the published onboarding documentation accurate

**Confirmed gap.** [The API guide](easy-mapping.md) still describes 1.1.0 as being
prepared locally, installs version 1.1.0, and directs consumers to a local feed until
publication. Both package projects embed this guide as their NuGet README, so new
1.1.1 packages also carry that text. Package verification checks that the README
exists; it does not detect stale release instructions.

**Acceptance:** provide a published-feed installation command, a complete minimal
example, its expected output, the supported compiler/SDK requirements, and the
approval workflow. Separate end-user installation from repository development.
Add a small standalone consumer sample using the package rather than the comparison
suite's project references. Compile the documented example in validation and check
version-bearing installation instructions when preparing a release.

Published packages are immutable; correct the source documentation and ship the
updated package README with a new version.

## Before claiming broader production support

| Priority | Work | Acceptance evidence |
| --- | --- | --- |
| High | Publish a support matrix | State tested SDK/compiler hosts, C# language versions, consumer target frameworks, operating systems, and IDEs. Distinguish package target compatibility from executed consumer tests. |
| High | Exercise the minimum supported compiler host | Restore the packed package in isolated projects under each promised SDK; compile and run mappings and the received/approved workflow. Current CI installs only .NET 10, and compiler tests use Roslyn 4.8/C# 12. |
| High | Validate real adoption | Migrate one actual mapping family in a consuming application. Preserve outputs, ignored sensitive/server-owned fields, null behavior, collection ownership, and update semantics with consumer tests. Current comparison models are synthetic. |
| Medium | Test build and editor integration | Verify clean, incremental, design-time, and multi-target builds, rebuild after approval edits, and independent approval directories when target frameworks have different shapes. Include an IDE smoke check for each advertised IDE. |
| Medium | Measure generator cost | Record clean-build time, single-model and unrelated-edit rebuild time, memory, and cancellation behavior with hundreds of mappers and approval files. Set regression budgets from measurements. |
| Conditional | Validate trimming and Native AOT | If advertised, publish and execute a packed consumer with those settings and review warnings. Direct generated calls alone do not establish tested support. |

The existing [incremental tests](../tests/XBullet.EasyMapping.Generator.Tests/IncrementalGenerationTests.cs)
accept either `Unchanged` or `Cached` output after an unrelated edit. This protects
output stability but does not prove that semantic planning was skipped or that large
solutions remain responsive. Existing [benchmarks](performance.md) measure mapping
execution; they do not measure compiler or editor cost.

## Product operation and discoverability

- State the product promise prominently: typed DTO mappings, explicit copy/null
  semantics, strict source/target accounting, and optional model-shape review.
- Add a troubleshooting guide organized by EMAP diagnostic, with failing/fixed
  examples and generated-source inspection instructions. Document converters as
  the escape hatch for unsupported conversions.
- Add a migration cookbook covering AutoMapper null defaults, collection replacement,
  immutable DTOs, nested pairs, instance converters, and domain update boundaries.
- Define how to report bugs and security issues, what information a minimal
  reproduction needs, how compatibility is maintained, and which versions receive
  fixes. Add focused issue templates and contribution instructions.
- Confirm the deployed branch/environment protections against the recorded policy
  and run a fresh consumer against the public NuGet feed after each release.
  Checked-in workflow/configuration files establish intended behavior, not live
  service configuration.

## Features to select from consumer demand

These are currently unsupported or restricted capabilities, not automatic blockers
for a production-quality DTO mapper:

1. Dictionary, set, and immutable-collection targets; explicit enum conversion
   policies and nested member paths.
2. Shared converters or mapper composition across classes, where duplicated local
   converter methods become a demonstrated adoption cost.
3. Query projections, only as a separate feature with relational-provider tests,
   SQL inspection, and clearly documented translation limits.
4. Runtime polymorphism, graph reference preservation, cyclic graphs, and
   presence-aware PATCH mapping, only after defining their contracts and value.

Keep explicit converters available and unsupported shapes diagnosable. Preserve
the current library boundaries; adding application/API/infrastructure projects or
a universal runtime mapper abstraction would not resolve these readiness gaps.

## Suggested delivery order

1. **Next patch:** fix the three confirmed defects, add their regressions, refresh
   onboarding/package documentation, and explain approval-baseline changes.
2. **Adoption milestone:** agree on a support matrix, add its package/SDK tests,
   validate one real consumer, and establish compiler-performance evidence.
3. **Next feature release:** select the most frequent consumer limitation, implement
   its explicit semantics, and extend documentation and package-level tests.

The practical readiness gate is a supported consumer installing the public package,
completing the guide, getting actionable diagnostics for mistakes, reviewing model
changes reliably, and building and running reproducibly on every promised platform.

## Validation and limits of this review

- Locked solution restore succeeded.
- Release solution build succeeded with zero warnings and zero errors.
- Generator/compiler/attribute suite: **159 passed**, zero skipped.
- Comparison/performance-harness suite: **94 passed**, zero skipped.
- Formatting verification exited successfully, with workspace-load warnings.
- Documentation validation passed: **50 local links across 14 Markdown files**.
- Isolated Roslyn probes reproduced the three defects described above. A probe for
  a private property hiding an inherited public property compiled successfully and
  is not reported as a defect.
- Package content/metadata checks and isolated packed-consumer execution passed.
  Received/approved integration passed for Source, Project, Custom, Existing,
  Linked, and External locations, including discovery and duplicate rejection.

Windows sandbox restrictions prevented the initial build and test runs from
completing normally; the successful build/tests used normal Windows access.
No implementation fixes, new feature support, or external repository changes were
made during this assessment. The probes live under ignored `artifacts/readiness-probes/`.
Live GitHub release/CI/settings and public NuGet state could not be independently
read in this session; the review does not certify those services or Linux execution.
