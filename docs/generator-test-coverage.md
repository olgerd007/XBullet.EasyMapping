# Generator test coverage

This is execution coverage of the generator implementation. The mapper's
`MappingCoverage.Both` option separately checks whether source and target members
are accounted for during compilation; it does not measure test execution.

## Reproduce the measurement

Use the Release configuration, matching CI. A fresh results directory prevents
the coverage gate from accidentally reading an earlier run:

```powershell
$branchResults = Join-Path artifacts/test-results ([Guid]::NewGuid().ToString('N'))
dotnet test tests/XBullet.EasyMapping.Generator.Tests/XBullet.EasyMapping.Generator.Tests.csproj --no-restore --configuration Release --verbosity minimal --collect "XPlat Code Coverage" --settings tools/coverage.runsettings --results-directory $branchResults
./tools/Test-Coverage.ps1 -ResultsDirectory $branchResults
```

The October 30, 2026 audit passed **474 generator tests**, with:

| Assembly | Line coverage | Branch coverage |
| --- | --- | --- |
| XBullet.EasyMapping.Generator | 99.68% | 97.25% |
| XBullet.EasyMapping.Abstractions | 100% | 100% |

Coverage settings, exclusions, and CI thresholds were unchanged. Tests exercise
creation and updates, nested source/target paths, nullable/struct intermediates,
constructor and required-member rules, constants and providers, collection
contracts, invalid declarations/configuration, model-approval serialization,
imported model assemblies, cancellation, and cached value equality and diagnostic
locations. Test models use orders and customers independently of consumer code.

## Residual branches

The report is **not 100%**. Across both assemblies, Coverlet records 920 of 946
branch outcomes. The 26 remaining outcomes fall into these groups:

| Code | Uncovered outcome and constraint |
| --- | --- |
| `ConstantValueBuilder` | Missing types on non-error, non-null constants; invalid array ranks after exact type matching; non-null array elements without types; `Type` constants without a type-symbol value. C# attribute arrays are one-dimensional, and malformed constants take the earlier error path. |
| `MappingPlanner` and `SymbolNames` | A non-class syntax declaration after validating a non-record class; a non-named root after DTO validation; a named symbol with a null `OriginalDefinition`. Earlier validation and Roslyn's symbol contracts prevent these paths. |
| Attribute readers | Null `AttributeClass` checks on the resolved attributes returned by the tested Roslyn compiler. Missing or invalid arguments are separately exercised as compiler recovery cases. |
| `ModelShapeBuilder` | Missing syntax metadata on the source mapper attribute; arbitrary non-attribute objects or null `ToString()` results in constant serialization. The generator discovers source mapper declarations, and supported attribute constants use the preceding scalar cases. |
| `MappingPlanner` configuration filters | Compiler-generated captured-delegate cache checks in three `Where` expressions. Inspection of emitted IL shows a fresh closure per method invocation and a single visit to each cache site, so the already-populated branch cannot run. |

The defensive checks remain in production. The tests do not fabricate contradictory
Roslyn symbols or suppress instrumentation to force a 100% number. Re-audit these
entries when upgrading Roslyn, changing compiler configuration, or reusing the
builders with new input contracts; compiler-generated branch counts can differ
between Debug and Release builds.
