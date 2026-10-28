# EasyMapping 1.1.0: generated DTO mappings

EasyMapping generates C# implementations of typed partial methods during compilation.
It owns its generator and does not delegate to AutoMapper, Mapperly, or Facet. The
initial implementation focuses on DTO transformations; EF projections are deferred.

Version 1.1.0 is being prepared for release and is currently built locally. It is not an API-compatible
replacement for every AutoMapper feature. The sample targets .NET 10; compiler tests
use Roslyn 4.8 and C# 12. The generator and attribute libraries target .NET Standard
2.0 so the compiler tooling is independent of the application's target framework.
Other consumer frameworks and compiler hosts have not been validated yet.

## Declare a mapper

```csharp
using XBullet.EasyMapping;

public sealed record PersonInput(int Id, string Name, List<string>? Tags);
public sealed record PersonDto(int Id, string DisplayName, List<string>? Tags);

[Mapper]
public static partial class PersonMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    public static partial PersonDto? ToDto(PersonInput? source);
}

```

Call it with `PersonMappings.ToDto(input)`.

Use a top-level, non-generic partial class. Both static mappers and instance mappers
are supported. Instance mapper constructors and dependencies remain normal C#.
Source and destination DTOs do not need mapping annotations.

Generated methods use direct constructors, initializers, assignments, and LINQ
collection materialization. They do not perform runtime type discovery or reflection.
Configuration attribute usages are omitted from emitted metadata by default; define
`MAPPER_PRESERVE_ATTRIBUTES` only if runtime inspection is required, retaining
the attributes assembly in that case.

## Semantics

- Target coverage is strict: every public target property must map, be supplied
  through the selected constructor, or be explicitly ignored. Extra source members
  may remain unused with the default `MappingCoverage.Target`. Opt into
  `MappingCoverage.Both` to require source coverage as well.
- Names match exactly first, then without case to support constructor parameter
  conventions. Names differing only by case within one DTO are rejected. Renames
  and ignores use direct names; nested member paths are not supported yet.
- A null root returns null when both source and result are nullable. A non-nullable
  result requires a source value; a null argument throws `ArgumentNullException`.
- Nullable properties map to nullable targets. Nullable-to-non-nullable member
  mappings require a handwritten converter that explicitly handles null.
- Null collections stay null. Supported collections always get a new container.
  Collection interfaces are materialized eagerly as lists, not lazy sequences.
- Strings and identical value types copy by assignment. Implicit numeric widening
  is supported. Other conversions require a declared mapping or handwritten method.
  Struct assignment is a value copy, not a promise to clone references inside structs.
- Mutable reference properties require an explicit nested mapping or converter,
  including when source and destination reference types are the same.

Supported collection targets are one-dimensional arrays, `List<T>`, `IList<T>`,
`ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, and `IEnumerable<T>`.
Sources can be one-dimensional arrays or generic enumerables. Element mappings use
the same null/conversion rules. Dictionaries as destinations, multidimensional
arrays, and queryable projections are outside this version's supported surface.

## Strict coverage of both models

```csharp
public sealed record ContactInput(int Id, string Name, string InternalNote);
public sealed record ContactDto(int Id, string DisplayName);

[Mapper(Coverage = MappingCoverage.Both)]
public static partial class ContactMappings
{
    [MapProperty(nameof(ContactInput.Name), nameof(ContactDto.DisplayName))]
    [MapIgnoreSource(nameof(ContactInput.InternalNote))]
    public static partial ContactDto Map(ContactInput source);
}
```

`Both` applies to every generated partial mapping in the mapper, including nested
methods and existing-target updates. Each readable public instance source property,
including inherited properties, must be used by a selected constructor argument or
a target assignment, or listed in `MapIgnoreSource`. One source property may supply
several targets. A source property passed to a handwritten converter counts as used;
the generator does not inspect that converter's implementation or inner object graph.

The original target-coverage rules still apply. `MapIgnoreTarget` does not acknowledge
an unused source property: if neither side should participate, ignore both sides
explicitly. A property cannot be both mapped and ignored. Source ignores use exact
property names, preferably `nameof`, and are validated even in `Target` mode.
Invalid, removed, duplicate, or contradictory ignores produce `EMAP006`.

An unaccounted source property produces `EMAP010` and prevents compilation. Adding
an unused source property, removing a target while leaving its source, or switching an
existing mapper to `Both` triggers revalidation. Strict mode rejects public source
fields and indexers because this version supports property mappings only. Static
and unreadable properties are outside source coverage.

The default remains `MappingCoverage.Target`; no migration is required for mappers
that intentionally consume only a subset of their source. `MapIgnoreSource` can
also document deliberate omissions in that mode.

Matching additions to both DTOs can still map by convention without an explicit
acknowledgment. Strict coverage ensures properties are accounted for, not that every
structural change is reviewed or that the mapping has the intended business meaning.
Enable the separate model-approval option below to require explicit acknowledgment
of structural changes that would still satisfy coverage.

## Model-shape approval

```csharp
[Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
public static partial class PersonMappings
```

Approval is disabled by default and is independent of coverage. Enabling it requires
one readable baseline per mapper, named with its fully qualified mapper name, for
example `MappingComparison.EasyMapping.PersonMappings.mapper.approved.txt`.
The generic filename suffixes are `.mapper.approved.txt` and `.mapper.received.txt`.
When migrating an existing baseline, rename the file; its contents do not change
and do not need to be reapproved.
By default, approval files sit beside the source file containing `[Mapper]`. The
package automatically includes `*.mapper.approved.txt` files throughout the project
as compiler `AdditionalFiles`, so no project glob is needed. It also includes
baselines directly inside an explicitly configured `MapperApprovalDirectory`,
including a shared folder outside the project. Build output and intermediate
folders, along with the project's default item exclusions, are skipped.

The compiler compares the current shape to that file. Missing, changed, duplicate,
or unreadable approvals cause **EMAP011** and fail the build. Even adding the same
property to both DTOs, removing matching properties, or changing both sides from
`int` to `long` requires a new approval. A model edit in another source file also
invalidates the approval. An unrelated source edit does not change the snapshot.

The baseline contains deterministic text describing generated mapping signatures,
coverage, mapping attributes, and reachable custom model shapes. Shapes include
public instance properties (also ignored and inherited properties), types and
nullability, getter/setter accessibility, init/required flags, public constructor
parameters and their defaults, public fields/constants, enum values, base/interface
identities, and application attributes. Property ordering and OS line endings do
not create approval changes. Recursive generic definitions are bounded by their
definition identity rather than expanded indefinitely.

Framework types are opaque leaves, with generic arguments still inspected. Compiler
implementation attributes, private state, method/constructor bodies, and property
initializer expressions are outside this public-shape snapshot. Handwritten mapping
behavior still requires tests. This is a structural review gate, not proof of business
correctness or an authenticated record of who reviewed a change.

### Review and approve

1. Build normally. A missing or changed baseline fails the build with **EMAP011**
   and writes `<fully-qualified-mapper>.mapper.received.txt` beside the mapper source.
2. Review the received file, comparing it with the existing approved file if present.
   Fix mapping rules or unintended model changes before accepting it.
3. Rename `.mapper.received.txt` to `.mapper.approved.txt`, replacing the
   old approved file when updating an existing baseline.
4. Build again and commit the approved file alongside the model/mapping changes.

For example:

```text
Mappings/
  PersonMappings.cs
  MyApp.PersonMappings.mapper.approved.txt  <- previous accepted shape
  MyApp.PersonMappings.mapper.received.txt  <- current shape to review
```

Received files are plain text in exactly the approved format. Builds refresh them
when approval is needed and remove them when the corresponding approved snapshot
matches. They never overwrite approved files. Keep `*.mapper.received.txt`
out of source control; commit only approved snapshots. Received files are not
`AdditionalFiles` and cannot satisfy approval until renamed and discovered on the
next build.

The NuGet package imports the MSBuild integration automatically. With direct
analyzer project references, also import the targets as the repository sample does:

```xml
<Import Project="../../src/XBullet.EasyMapping.Generator/buildTransitive/XBullet.EasyMapping.targets" />
```

Adjust that path for your project. The integration runs on ordinary builds,
including failed compilation, and skips IDE design-time builds. It uses the SDK's
Roslyn MSBuild task factory; PowerShell and Git are not needed for this workflow.
The generator itself remains free of filesystem writes and also emits the current
shape as comments in `*.ModelApproval.g.cs`. Invalid mapping plans must be fixed
before they can produce a fresh approval candidate.

For a partial mapper, the default location is the file containing `[Mapper]`, even
if its methods are declared in other files. When an existing approved baseline is
already included elsewhere inside the project, received files stay beside that
baseline. Existing `MappingApprovals/` folders therefore continue to work without
moving or reapproving their contents. A linked mapper source outside the project
falls back to the project root so ordinary builds do not write into another checkout.

Set `MapperApprovalDirectory` to put new received files in one project folder
instead. Relative paths are resolved against the directory containing the `.csproj`.
For example, to keep approvals beside the project file:

```xml
<PropertyGroup>
  <MapperApprovalDirectory>$(MSBuildProjectDirectory)</MapperApprovalDirectory>
</PropertyGroup>
```

Use `MappingApprovals` as the value for a shared review folder. An explicit setting
takes precedence over both the source and existing baseline locations. Move any
existing approved files to the chosen folder; builds never move or overwrite them.
Discovery includes these locations automatically and avoids adding files already
listed explicitly as `AdditionalFiles`. Distinct files with the same mapper name
still cause **EMAP011**; discovery never chooses one approval over another.

For configurations or target frameworks with different model shapes, disable
default discovery and select only the intended baselines. For example:

```xml
<PropertyGroup>
  <EnableDefaultMapperApprovalItems>false</EnableDefaultMapperApprovalItems>
  <MapperApprovalDirectory>MappingApprovals/$(Configuration)</MapperApprovalDirectory>
</PropertyGroup>
<ItemGroup>
  <AdditionalFiles Include="$(MapperApprovalDirectory)/*.mapper.approved.txt" />
</ItemGroup>
```

Manual `AdditionalFiles` entries also remain available for individual external
baselines. When importing the targets directly, put that import after the project
properties and explicit items, as shown in the sample.

The existing PowerShell helper remains available for reviewing a Git diff or for
consumers without the MSBuild import:

```powershell
./tools/Export-ModelApprovals.ps1 -Project samples/MappingComparison.EasyMapping/MappingComparison.EasyMapping.csproj
```

It uses the same directory rules as ordinary builds and exits with a failure when
review is needed. Review and rename the received files, then rebuild. The optional `-Approve` switch
still writes approved files and rebuilds after review. This helper requires
PowerShell 7, Git, a restored project and built project references; it refuses
unrelated build errors and is included under the package's `tools/` directory.

Normal builds and CI never accept changes automatically.
Internal markers and settings use generic mapper names: `MapperSourcePath`,
`MapperModelApproval`, `MapperApprovalDirectory`, and
`EnableDefaultMapperApprovalItems`. The shipped projects, helper scripts, and
configuration examples use these names. The public `XBullet.EasyMapping` namespace
and package identities are unchanged.
Removing a mapper does not automatically delete its old baseline; remove such files
explicitly during review. The generator reads only compiler `AdditionalFiles`; the
imported build integration supplies them automatically. Using the analyzer without
that integration still requires explicit `AdditionalFiles` entries.

## Nested DTOs and custom conversions

Declare each nested pair in the same mapper:

```csharp
private static partial AddressDto ToAddress(AddressInput source);
```

Handwritten methods can provide additional conversions, including nullable input
handling or formatting. They must have one input and a return value with compatible
types. Generic, asynchronous, and ref/out conversions are not discovered. Exact
signatures are preferred; competing candidates produce a diagnostic rather than
depending on declaration order. Instance converters cannot serve a static mapping.
Keep conversions pure and perform external I/O in application use cases.

Generated mapping dependency cycles are rejected before runtime. Reference tracking,
automatic graph truncation, and arbitrary cyclic graphs are not supported. Explicit
handwritten graph mappings remain the consumer's responsibility.

## Constructor and required-member rules

Creation prefers a public parameterless constructor. Otherwise it requires exactly
one public constructor whose non-optional parameters have source properties.
Missing optional arguments retain their declared defaults. Renames can address
constructor parameter names; configuration for unselected parameters is rejected.

`init` and `required` properties are supported during creation. Required members
are initialized even when a matching constructor parameter exists, unless the
constructor declares `SetsRequiredMembers`. A required member cannot be ignored
without that constructor guarantee. Read-only properties must be supplied through
the constructor or explicitly ignored. Constructors with competing viable shapes
need a handwritten mapping in this version.

## Update an existing mutable DTO

```csharp
[MapIgnoreTarget(nameof(EditableProfile.Id))]
[MapIgnoreTarget(nameof(EditableProfile.ServerNote))]
public static partial void Update(ProfileUpdate source, EditableProfile target);
```

Both root arguments are required. Writable target properties are assigned, including
explicit null values; ignored values remain unchanged. Init-only members cannot be
assigned by updates. Update targets must be reference types. Collection containers
are copied and replaced, so previous list references are not preserved.

This is replacement-style DTO mapping. It does not distinguish omitted fields from
explicit null and does not enforce aggregate business rules or PATCH semantics.

## Diagnostics

All mapping diagnostics are errors and identify the declaration to fix:

| ID | Meaning |
| --- | --- |
| EMAP001 | Mapper must be a supported partial class |
| EMAP002 | Unsupported mapping signature or root type |
| EMAP003 | Target member has no readable source property |
| EMAP004 | No suitable constructor, or ambiguous constructor selection |
| EMAP005 | Unsupported conversion or unsafe nullable member mapping |
| EMAP006 | Invalid, conflicting, or unused member configuration |
| EMAP007 | Ambiguous mapping/conversion methods |
| EMAP008 | Recursive generated mapping dependencies |
| EMAP009 | Unsupported member, such as target fields/indexers, unwritable properties, or strict-mode source fields/indexers |
| EMAP010 | Source property is neither mapped nor explicitly ignored under `Both` |
| EMAP011 | Required model-shape approval is missing, changed, duplicated, or unreadable |

No partial mapper implementation is emitted when its mapping plan has errors. The
compiler may additionally report that the declared partial methods lack implementations.
For approval-only errors the mapping and review candidate are emitted, but EMAP011
still prevents the compiler from producing a successful build.

## Build, test, and package

The solution's normal restore/build validates the generator as an analyzer project
reference. Run both the comparison and compiler test projects as documented in the
repository README. Inspect emitted source in
`samples/MappingComparison.EasyMapping/obj/generated/`; reviewed copies are kept
under `docs/evidence/generated/EasyMapping/`.

`tools/Test-Package.ps1` packs `XBullet.EasyMapping` and
`XBullet.EasyMapping.Abstractions` into `artifacts/packages/1.1.0`, verifies package
metadata, MIT license, README, dependency versions, and compiler integration assets, then restores an isolated
consumer from that local feed, and executes it. The smoke consumer is deliberately
outside the solution so an ordinary solution restore does not need unpublished
packages to exist first. Each smoke run uses a fresh cache to prevent stale package
contents from making the test pass.

Install the locally built generator package in a consumer with:

```xml
<PackageReference Include="XBullet.EasyMapping" Version="1.1.0" />
```

The package carries the generator under `analyzers/dotnet/cs` and a dependency on
the configuration attributes. Roslyn is a private compiler dependency and is not
shipped as a consumer runtime library. No package is published by the smoke script.

Until the release is published, use `artifacts/packages/1.1.0` as the local NuGet
source. The package includes its MIT license and a source repository/commit link.
