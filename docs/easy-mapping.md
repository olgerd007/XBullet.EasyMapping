# EasyMapping: generated DTO mappings

EasyMapping generates C# implementations of typed partial methods during compilation.
It owns its generator and does not delegate to AutoMapper, Mapperly, or Facet. The
initial implementation focuses on DTO transformations; EF projections are deferred.

Published packages are available from [NuGet](https://www.nuget.org/packages/XBullet.EasyMapping).
EasyMapping is not an API-compatible replacement for every AutoMapper feature.
Consumer builds are tested with the .NET 10 SDK; compiler tests use Roslyn 4.8 and
C# 12. The generator and attribute libraries target .NET Standard 2.0 so compiler
tooling is independent of the application's target framework. Other consumer
frameworks and compiler hosts have not been validated yet.

## Install and run

Create a console application with the .NET 10 SDK and install the latest published
package from NuGet:

```shell
dotnet new console --framework net10.0 --name EasyMappingDemo
cd EasyMappingDemo
dotnet add package XBullet.EasyMapping
```

The package includes the generator and its configuration attributes. No mapper
registration or runtime mapping service is needed. Replace `Program.cs` with:

```csharp
using System;
using System.Collections.Generic;
using XBullet.EasyMapping;

var source = new PersonInput(42, "Ada", ["original"]);
var dto = PersonMappings.ToDto(source);
source.Tags!.Add("changed after mapping");
Console.WriteLine($"{dto.Id}: {dto.DisplayName} ({string.Join(", ", dto.Tags!)})");

public sealed record PersonInput(int Id, string Name, List<string>? Tags);
public sealed record PersonDto(int Id, string DisplayName, List<string>? Tags);

[Mapper]
public static partial class PersonMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    public static partial PersonDto ToDto(PersonInput source);
}
```

Run `dotnet run`. The output is `42: Ada (original)`: the mapped list has its own
container, so the later source mutation does not change the DTO.

## Declare a mapper

Use a top-level, non-generic partial class without the `file` modifier. Generated
source cannot complete a file-local class from another file; these declarations
produce EMAP001. Both static mappers and instance mappers are supported. Instance
mapper constructors and dependencies remain normal C#.
Source and destination DTOs do not need mapping annotations.

Generated methods use direct constructors, initializers, assignments, and collection
materialization. Identity element mappings copy directly; converted list mappings
from arrays and standard counted collection contracts use capacity-sized loops.
Other converted collection mappings retain LINQ materialization.
They do not perform runtime type discovery or reflection.
Configuration attribute usages are omitted from emitted metadata by default; define
`MAPPER_PRESERVE_ATTRIBUTES` only if runtime inspection is required, retaining
the attributes assembly in that case.

## Semantics

- Target coverage is strict: every public target property must map, be supplied
  through the selected constructor, or be explicitly ignored. Extra source members
  may remain unused with the default `MappingCoverage.Target`. Opt into
  `MappingCoverage.Both` to require source coverage as well.
- Names match exactly first, then without case to support constructor parameter
  conventions. Names differing only by case within one DTO are rejected.
  `MapProperty` accepts direct or dot-separated source and target property paths.
  `MapConstant`, `MapGeneratedValue`, and `MapIgnoreTarget` also accept target
  paths. `MapIgnoreSource` names a direct source property.
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

### Explicit values and nested coverage

Constants and generated values satisfy target coverage without consuming a source
property. Replacing a source value therefore needs `MapIgnoreSource` in `Both`
mode, even when source and target have the same name:

```csharp
public sealed record OrderInput(int Id, string CustomerName, string InternalNote);
public sealed record CustomerDto(string Name, string SourceSystem);
public sealed record OrderDto(int Id, CustomerDto Customer);

[Mapper(Coverage = MappingCoverage.Both)]
public static partial class OrderMappings
{
    private static int CreateOrderId() => 7;

    [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(CreateOrderId))]
    [MapIgnoreSource(nameof(OrderInput.Id))]
    [MapIgnoreSource(nameof(OrderInput.InternalNote))]
    [MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
    [MapConstant("Customer.SourceSystem", "ORDERS")]
    public static partial OrderDto Map(OrderInput source);
}
```

Here `CustomerName` is consumed, `Id` and `InternalNote` are explicitly ignored,
and all target members are supplied. The helper returns a fixed ID only to keep
the example deterministic; use the application's ID policy in a real mapping.
Removing the source `Id` ignore produces EMAP010. Adding an unmapped target
property such as `Customer.Version` produces EMAP003 naming that full path.

| Configuration | Source coverage | Target coverage |
| --- | --- | --- |
| Same-name assignment or selected constructor argument | Consumes its source property | Covers the assigned target |
| `MapProperty("Customer.Name", "CustomerName")` | Consumes the root `Customer` | Covers `CustomerName` |
| `MapProperty("CustomerName", "Customer.Name")` | Consumes `CustomerName` | Covers `Customer.Name`; other child members still need configuration |
| `MapProperty(..., Use = nameof(ConvertName))` | Consumes the selected source root | Covers the selected target; helper internals are not inspected |
| `MapProperty("FullName", Use = nameof(BuildName), Sources = new[] { "FirstName", "LastName" })` | Consumes the declared source roots | Covers the selected target using the whole source; helper internals are not inspected |
| `MapConstant` / `MapGeneratedValue` | Consumes nothing | Covers the configured target |
| `MapIgnoreSource("Name")` | Acknowledges an unused source root | Covers nothing |
| `MapIgnoreTarget("Customer.Version")` | Consumes nothing | Acknowledges the omitted target leaf |

Nested source paths account for their root rather than every descendant. Nested
target paths explicitly construct or update the child, so **each public child
property must be mapped, constructor-supplied, or ignored**. Child names do not
implicitly match unrelated properties on the root source. Use a separate generated
child mapping when convention-based matching within the child model is wanted;
that method gets its own coverage check.

Ignoring a whole target subtree, such as `Customer`, acknowledges that subtree.
It cannot be combined with mappings or ignores below `Customer`. A whole-object
assignment also cannot be combined with descendant configurations. Invalid paths
and contradictory configurations produce EMAP006. Required-member and constructor
rules still apply to ignored members.

Coverage checks generated partial declarations only. A handwritten wrapper can
call a generated mapping and then change its result; those manual assignments and
the business meaning of helper bodies need runtime unit tests. Coverage here is
compile-time member accounting, separate from test line or branch coverage.

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
parameters and their defaults, public fields/constants, enum underlying types and
values, base/interface identities, and application attributes. Property ordering and OS line endings do
not create approval changes. Recursive generic definitions are bounded by their
definition identity rather than expanded indefinitely.

Starting with 1.1.2, enum shapes include their actual underlying type: `byte`,
`sbyte`, `short`, `ushort`, `int`, `uint`, `long`, or `ulong`. For example,
`enum Status : byte` records `underlying byte`; an enum without an explicit
underlying type records `underlying int`. Changing the underlying type requires
approval even when enum values are unchanged. When upgrading from an earlier version, existing
approvals containing enums fail with EMAP011 once: review the new received file
and replace the approved baseline using the workflow below. Approvals without
enums and the v1 text-format header remain unchanged.

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

Use a source path to flatten a nested property into a target property or
constructor argument:

```csharp
[MapProperty(
    nameof(OrderInput.Customer) + "." + nameof(Customer.Status),
    nameof(OrderDto.CustomerOrderStatus))]
public static partial OrderDto Map(OrderInput source);
```

Each path segment must name an exact public readable instance property, including
inherited properties. Fields, indexers, and static or inaccessible properties are
rejected with EMAP006 identifying the path and failing segment. This example
configures one member; the other target
members still need matching source properties, explicit mappings, or ignores.

Conversions apply to the selected leaf property. The same enum type copies
directly; different enum types and enum-to-number conversions require an explicit
converter. Nullable intermediate references and structs propagate null through
the path, making its result nullable even when the leaf is non-nullable. A
non-nullable target therefore requires a handwritten converter for the nullable
leaf type. Getters are evaluated once per path access; separate target mappings
evaluate their paths independently.

Under `MappingCoverage.Both`, a nested source path accounts for its root property
(for example, `Customer`), without requiring coverage of every member inside it.
The same root may supply several targets; it cannot also be ignored with
`MapIgnoreSource`. Changing a source path is included in model-shape approval.

### Nested target paths

Target paths can build children from flattened inputs or reshape nested inputs:

```csharp
[MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
[MapConstant("Customer.SourceSystem", "ORDERS")]
[MapIgnoreTarget("Customer.Version")]
public static partial OrderDto Map(OrderInput source);
```

Sibling paths share one child object. Creation uses the same constructor,
`init`, and required-member rules as root mappings, including immutable child
records and constructor parameters. Paths may span several levels. Each configured
child needs complete target coverage as described in
[strict coverage](#explicit-values-and-nested-coverage).

An existing-target update reuses existing child references, preserving ignored
members. A null intermediate is constructed using the same configuration; ignored
members then retain constructor or initializer defaults. Nullable and non-nullable
struct intermediates are copied back after updating. Intermediates in an update
require public getters and writable non-`init` setters, and the child must also be
constructible for the null case. Read-only or `init` leaves require a creation
mapping or a whole-object replacement instead of an in-place nested update.

Intermediates must be concrete DTO classes or structs. Collection indexing,
dictionary keys, scalar traversal, and automatic reference-cycle handling are
unsupported. Whole-object assignments and descendant configurations conflict
(EMAP006); unsupported or unwritable members produce EMAP009. Target path changes
participate in model-shape approval.

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

Use `MapProperty.Use` to bind a helper to one member instead of selecting it by
type pair alone:

```csharp
[MapProperty(
    nameof(OrderInput.Customer),
    nameof(OrderDto.CustomerStatus),
    Use = nameof(MapCustomerStatus))]
public static partial OrderDto Map(OrderInput source);

private static OrderStatus MapCustomerStatus(Customer? customer) =>
    customer?.Status ?? OrderStatus.Pending;
```

With the source-and-target overload, the helper receives the selected source
value, or the leaf value when a nested source path is used. Selecting the whole
object lets the helper check runtime
nulls before reading its members, including when a legacy source property lacks
a nullable annotation. The helper is called for null values too; its parameter
must accept null when the source path is nullable. Nullable helper results require
nullable targets. Collection helpers receive the whole collection, not individual
elements.

To compute a target value from several source properties, use the target-only
overload. Its `Use` helper receives the whole source object:

```csharp
[MapProperty(
    nameof(AdviserAmendmentServiceRequest.AdviserCompany),
    Use = nameof(MapAdviserCompany),
    Sources = new[]
    {
        nameof(ServiceRequestEntity.AdviserCompanyIsChanged),
        nameof(ServiceRequestEntity.AdviserCompanyValue),
        nameof(ServiceRequestEntity.AdviserCompanyApprovalStatus)
    })]
public static partial AdviserAmendmentServiceRequest Map(ServiceRequestEntity source);

private static AdviserCompanyFieldChange MapAdviserCompany(ServiceRequestEntity source)
{
    return new AdviserCompanyFieldChange
    {
        IsChanged = source.AdviserCompanyIsChanged,
        Value = source.AdviserCompanyValue,
        ApprovalStatus = source.AdviserCompanyApprovalStatus
    };
}
```

The target-only overload requires a non-empty `Use` name. It supports target paths
and constructor parameters as well as direct properties; no `MapIgnoreTarget` is
needed. Creation supplies the helper result during construction, including `init`
and required properties. Updates replace the configured member with the helper
result. Each configured whole-source helper is evaluated once per target per
mapping call; constructor and required-member initialization reuse that result.
Normal root null handling happens first: a nullable root/result mapping returns
null without invoking the helper, and other null reference roots throw. A nullable
value-type root is unwrapped before it is passed to the helper. The helper's input
must match the non-null root type, optionally accepting null as well.

The optional `Sources` array declares which source root properties the helper
consumes. Those properties count as mapped under `MappingCoverage.Both`, so the
example accounts for all three inputs without `MapIgnoreSource`. Other source
properties still need a mapping or an explicit ignore. Without `Sources`, or with
an empty array, the whole-source binding consumes no individual source properties.
The generator does not inspect helper bodies or automatically mark every source
property as used; declarations describe intended use rather than prove it.

`Sources` is supported only by the target-only overload with `Use`. Entries must
be exact names of public readable instance source root properties, preferably
written with `nameof`; inherited properties are supported. Nested paths, null
arrays or entries, invalid names, and duplicate entries within one binding produce
EMAP006, even in target-only coverage mode. A declared source cannot also be
ignored, but may supply other generated assignments or several helpers. Changing
the declarations participates in model-shape approval. Declaring a property does
not generate a read of its getter.

The `Source` property of a target-only attribute is an empty string when attributes
are preserved for runtime inspection.

`Use` must name a synchronous, non-generic, single-input method in the same mapper
with no `ref`/`out` parameter, by-reference result, or `dynamic` parameter/result.
Its parameter and return types
must match the source and target types, allowing safe outer nullable lifting but
not different nested generic nullability or implicit numeric/reference adapters.
Instance helpers require an instance mapping. Generated partial mapping methods
can also be selected, subject to the normal signature and cycle rules.

An exact type-and-nullability overload is preferred; otherwise exactly one
compatible overload must exist. Invalid names, incompatible signatures, and
ambiguous overloads produce EMAP006 naming the helper, target, and mapping. There
is no fallback to automatic conversion for an invalid explicit binding.

Every helper name referenced by `Use`, including all overloads of that name, is
excluded from automatic converter discovery throughout that mapper. Bind it
explicitly on each member that needs it. Other automatic helpers and mappings
without any `Use` configuration retain their existing behavior. A source property
selected by the two-argument overload still counts toward strict coverage, and
helper-name changes participate in model approval; helper method bodies remain
outside the model-shape snapshot.

Generated mapping dependency cycles are rejected before runtime. Reference tracking,
automatic graph truncation, and arbitrary cyclic graphs are not supported. Explicit
handwritten graph mappings remain the consumer's responsibility.

## Constant and generated values

Use `MapConstant` when a target property or constructor parameter gets a fixed
value instead of a source member. Use `MapGeneratedValue` for a parameterless
helper evaluated when the mapper is called:

```csharp
[MapConstant(nameof(OrderEntity.CreatedBy), "SYSTEM")]
[MapConstant(nameof(OrderEntity.Status), OrderStatus.Pending)]
[MapGeneratedValue(nameof(OrderEntity.Id), Use = nameof(CreateOrderId))]
public static partial OrderEntity ToEntity(OrderInput source);

private static Guid CreateOrderId() => Guid.NewGuid();
```

Constants support C# attribute scalar values (strings, characters, booleans,
integer and floating-point types, and enums), `typeof(...)`, and null for nullable
targets. Non-null constants must have the same underlying type as the target;
nullable lifting is allowed, but numeric widening, boxing, and implicit enum
casts are not. Floating-point special values and signed zero are preserved.
One-dimensional attribute arrays are also supported, with the same array type as
the target and compatible element nullability:

```csharp
[MapConstant(nameof(OrderDto.Labels), new string[] { "new", "web" })]
```

Each configured target gets a fresh array on each mapping call, including updates;
constructor and required-member initialization share that target's array. Explicit
`object[]` values may contain boxed attribute-compatible elements. Scalar constants
still require an exact type rather than implicit boxing. `decimal`, `Guid`,
`DateTime`, and other values that cannot be supplied as C# attribute constants
should come from a generated-value helper instead. Strings and characters are
emitted as escaped C# literals.

`MapGeneratedValue.Use` must name a synchronous, non-generic, parameterless method
in the same mapper. Its return type must match the target type with safe
nullability; `dynamic` and by-reference results are unsupported. Instance helpers
require an instance mapper method. A provider runs at runtime, after root null
guards and before target construction or update assignments, once per configured
target on each call. If a required member is supplied through both a constructor
and its initializer, they share one generated value. Different targets using the
same helper still invoke it separately. Updates also regenerate configured values;
only configure identifiers or audit fields there when replacement is intended.

These configurations cover the target without consuming any source property.
Under `Coverage.Both`, explicitly ignore any same-named source property that is
being replaced. Keep source conversion on `MapProperty.Use`; generated-value
helpers receive no source argument. Explicit constants and generated values bypass
automatic source converters. Helper names referenced by `MapGeneratedValue.Use`
are explicit-only throughout the mapper, like names referenced by `MapProperty.Use`.

Each target may have only one `MapProperty`, `MapConstant`, or `MapGeneratedValue`
configuration and cannot also be ignored. Targets may be direct property names,
constructor-parameter names, or dot-separated nested target paths. Invalid values,
names, helper signatures, and conflicting configurations produce EMAP006, while
unwritable target members retain EMAP009. Constant values and provider names are
included in model-shape approvals; helper bodies are not.

## Constructor and required-member rules

Creation prefers public constructors that can map every non-ignored property
without a public setter, including inherited properties. Among those constructors,
a public parameterless constructor is preferred when available; otherwise exactly
one constructor must qualify. Non-optional parameters must have source properties
or explicit `MapConstant`/`MapGeneratedValue` configurations.
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
Fix the accompanying `EMAP` diagnostic first: it points to the mapper or mapping
method in the source file. Missing-source errors name the target property and
suggest `MapProperty` or `MapIgnoreTarget`; conversion errors name the mapping,
member, source/target types, and the reason the conversion is unsupported. If only
CS8795 is reported, check that the generator is loaded, that the `[Mapper]`
attribute is recognized, and that no generator-loading or execution warnings are
being filtered out.
For approval-only errors the mapping and review candidate are emitted, but EMAP011
still prevents the compiler from producing a successful build.

## Build, test, and package

The solution's normal restore/build validates the generator as an analyzer project
reference. Run both the comparison and compiler test projects as documented in the
repository README. Inspect emitted source in
`samples/MappingComparison.EasyMapping/obj/generated/`; reviewed copies are kept
under `docs/evidence/generated/EasyMapping/`.

`tools/Test-Package.ps1` packs `XBullet.EasyMapping` and
`XBullet.EasyMapping.Abstractions` into `artifacts/packages/<version>`, using the
version from `Directory.Build.props`. It verifies package metadata, MIT license,
README, dependency versions, and compiler integration assets, then restores and
executes an isolated consumer from that local feed. The smoke consumer is deliberately
outside the solution so an ordinary solution restore does not need unpublished
packages to exist first. Each smoke run uses a fresh cache to prevent stale package
contents from making the test pass.

The package carries the generator under `analyzers/dotnet/cs` and a dependency on
the configuration attributes. Roslyn is a private compiler dependency and is not
shipped as a consumer runtime library. No package is published by the smoke script.

For repository development, install a locally packed candidate with
`dotnet add package XBullet.EasyMapping --version <version> --source <absolute-package-directory>`.
Use the version and directory produced by `tools/Test-Package.ps1`. This local-feed
workflow is separate from installing the published package above. The package
includes its MIT license and a source repository/commit link.
