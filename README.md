# XBullet.EasyMapping

EasyMapping now includes its own incremental C# source generator for typed DTO
mappings, alongside the AutoMapper/Mapperly/Facet comparison suite. EF projections
are deferred.

Read the [EasyMapping API and semantics](docs/easy-mapping.md) to use the first
implementation. The [comparison results](docs/dto-comparison-results.md) and
[initial investigation](docs/mapping-investigation.md) retain the earlier evidence.
Version **1.1.0** is the first published release of the owned generator.
Version **1.1.1** adds collection-mapping performance improvements and CI maintenance.
Version **1.1.2** fixes converter shadowing, file-local
diagnostics, enum approvals, and installation documentation.
Version **1.2.0** adds nested source paths, constructor
selection improvements, and navigable mapping diagnostics.
Version **1.3.0 (in preparation)** adds explicit converters, constant and generated
target values, and nested target paths for creation and updates.
See the [release guide](docs/release.md),
[release notes](CHANGELOG.md), and [MIT license](LICENSE).

NuGet publication uses the [release publishing workflow](.github/workflows/publish.yml)
after a GitHub Release is published and its `nuget` deployment is approved. It
reuses verified packages from the matching successful main CI run.

```csharp
using XBullet.EasyMapping;

[Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
public static partial class PersonMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    [MapIgnoreSource(nameof(PersonInput.InternalNote))]
    public static partial PersonDto? ToDto(PersonInput? source);
}
```

Declared nested methods, handwritten converters, immutable DTO construction, and
existing-target updates are supported. Null collections remain null; collection
containers are copied. Missing target mappings and unsafe conversions are errors.
`MapProperty.Use` explicitly selects a source converter; `MapConstant` and
`MapGeneratedValue` supply [fixed or generated target values](docs/easy-mapping.md#constant-and-generated-values).
Dot-separated target paths build or update nested children, and array constants
produce fresh arrays for each target and call.
[`MappingCoverage.Both`](docs/easy-mapping.md#strict-coverage-of-both-models) also
rejects unused source properties unless explicitly ignored. Constants and
generated values cover targets without consuming sources; nested target members
must each be accounted for. The default remains `MappingCoverage.Target` for existing mappers.
`RequireModelApproval` additionally blocks structural model changes until the
readable approval files are explicitly updated. See the [approval workflow](docs/easy-mapping.md#model-shape-approval)
for automatic baseline discovery and the received-file workflow. Approval is opt-in.

## Run

Requires the .NET 10 SDK and PowerShell 7 for the evidence scripts.

The repository's `NuGet.Config` uses nuget.org as its single package source,
avoiding `NU1507` when Visual Studio adds machine-level feeds. Package smoke tests
select their locally packed feed explicitly with `--source`.

```powershell
dotnet restore XBullet.EasyMapping.slnx --locked-mode
dotnet build XBullet.EasyMapping.slnx --no-restore --configuration Release --verbosity minimal -m:1
dotnet test tests/MappingComparison.Tests/MappingComparison.Tests.csproj --no-restore --no-build --configuration Release --verbosity minimal
dotnet test tests/XBullet.EasyMapping.Generator.Tests/XBullet.EasyMapping.Generator.Tests.csproj --no-restore --no-build --configuration Release --verbosity minimal
dotnet format XBullet.EasyMapping.slnx --no-restore --verify-no-changes --severity info --diagnostics IDE0011 IDE0055 IDE2003
dotnet format whitespace src/XBullet.EasyMapping.Generator/buildTransitive --folder --verify-no-changes
dotnet format whitespace tests/Diagnostics --folder --verify-no-changes
dotnet format whitespace tests/PackageSmoke --folder --verify-no-changes
```

The 94 comparison and performance-harness tests cover handwritten mapping,
AutoMapper 16.2.0, Mapperly 4.3.1, Facet 6.6.15, and our own generator.
The compiler, generator, and attribute-contract tests cover
generated execution, strict coverage, model approval, diagnostics, and incremental
invalidation. See the [generator coverage audit](docs/generator-test-coverage.md)
for the branch measurement, reproduction command, and residual compiler guards.
A compiled order/customer/line-item graph exercises nested copies,
nullable and jagged collections, required/init members, custom converters, and
replacement-style updates with typed xUnit assertions. AutoMapper also runs with
`AllowNullCollections`, and Mapperly with `UseDeepCloning`, so defaults and
configured behavior are distinguishable.

## Performance and load testing

Run `./tools/Invoke-PerformanceTests.ps1` for Release-mode BenchmarkDotNet timing,
allocation measurements, and sustained mapping at one and four workers across
the existing mapper variants and a frozen pre-optimization reference. See
[optimization results](docs/performance-optimization.md), the
[initial baseline](docs/performance-results.md)
and [performance methodology](docs/performance.md) for workload sizes,
collection-copy differences, options, and result locations.

## Inspect generated code and diagnostics

Generated code is emitted under each generator sample's `obj/generated/`. Reviewed
copies are stored as `.cs.txt` files in [generated evidence](docs/evidence/generated)
so they cannot accidentally become compile inputs.
Approval-comment files are excluded from these code snapshots; model approvals
have separate baselines beside the mapper sources.

Two intentionally invalid projects are excluded from the solution. Their failures
are verified separately, including diagnostic IDs and messages:

```powershell
dotnet restore tests/Diagnostics/Mapperly.UnmappedTarget/Mapperly.UnmappedTarget.csproj --locked-mode
dotnet restore tests/Diagnostics/Facet.InvalidInclude/Facet.InvalidInclude.csproj --locked-mode
./tools/Test-GeneratorDiagnostics.ps1
./tools/Export-GeneratedMappings.ps1 -Verify
./tools/Export-ModelApprovals.ps1 -Project samples/MappingComparison.EasyMapping/MappingComparison.EasyMapping.csproj -Configuration Release
```

After intentionally changing mappings or upgrading packages, inspect the output
and refresh evidence with `./tools/Test-GeneratorDiagnostics.ps1 -UpdateEvidence`
and `./tools/Export-GeneratedMappings.ps1`.

Model approvals are separate from generated-code evidence. Ordinary builds write
`<fully-qualified-mapper>.mapper.received.txt` beside the mapper source when a baseline is missing or changed
and fail with EMAP011. Review the received file, rename it to `.mapper.approved.txt`
(replace the existing approved file), then rebuild and commit the approved snapshot.
No approval command is needed. NuGet imports the build integration automatically;
direct analyzer project references also need the targets import shown in the sample.
The integration discovers approved files automatically; project `AdditionalFiles`
globs are unnecessary. Received files and build folders are excluded.
Existing included baselines elsewhere in the project keep their location. Set
`MapperApprovalDirectory` to use one project folder instead; see the
[location settings](docs/easy-mapping.md#review-and-approve).
CI never accepts changes automatically. The export script remains an optional diff tool.

## Layout

- `src/XBullet.EasyMapping.Abstractions`: compile-time configuration attributes.
- `src/XBullet.EasyMapping.Generator`: the owned incremental generator and diagnostics.
- `samples/MappingComparison.Contracts`: DTOs shared by the comparison, with no mapper dependency.
- `samples/MappingComparison.Manual`: explicit mapping and collection-copy baseline.
- `samples/MappingComparison.AutoMapper`: profile and validated AutoMapper configuration.
- `samples/MappingComparison.Mapperly`: default and copying generated mappings over existing DTOs.
- `samples/MappingComparison.Facet`: generated DTOs and explicit root-null boundary.
- `samples/MappingComparison.EasyMapping`: handwritten DTOs mapped by our generator.
- `tests/MappingComparison.Tests`: common behavior, serialization, aliasing, and update tests.
- `benchmarks/MappingComparison.Benchmarks`: isolated timing, allocations, and concurrent mapping load.
- `tests/Diagnostics`: expected compiler failures for configuration mistakes.
- `tests/XBullet.EasyMapping.Generator.Tests`: compiler and runtime regression tests.
- `tests/PackageSmoke`: consumer of the locally packed NuGet, excluded from the solution.

Library versions target .NET Standard 2.0; samples and tests target .NET 10. Direct
dependencies are centralized and solution project resolutions are recorded in lock
files. Sample projects are non-packable.

Run `./tools/Test-Package.ps1` to build both NuGet packages into
`artifacts/packages/1.3.0`, verify their metadata/license/assets, and execute an
isolated consumer using that local feed. The version comes from
`Directory.Build.props`; the smoke consumer uses the same version through central
package management. Each run uses a fresh cache.

CI is configured to verify Release builds, all 568 tests, formatting, documentation links,
diagnostics, model approvals, generated evidence, and packaged consumption on
Windows and Ubuntu. Test results are retained even on failure; a successful
Windows job also retains the verified packages. Both jobs must pass before
publishing. Public API approvals and package compatibility protect the attributes
contract against published 1.2.0. Generator coverage requires at least 95% lines
and 80% branches; attribute coverage requires 100%. NuGet auditing checks transitive
dependencies, and dedicated workflows run dependency review and CodeQL. Dependabot
proposes weekly dependency and pinned Actions updates.
Performance runs separately on demand; consumer-specific profiles and
other target frameworks remain outside the CI checks.
