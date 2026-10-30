# Releases and versioning

This document describes the release procedure for **1.4.0**, dated 2026-10-30.
NuGet publication is complete only when the publishing workflow succeeds. The
existing release tags and published package versions remain immutable. A `v<version>` tag and
GitHub Release identify the tested source commit; NuGet publication completes after approval of
the `nuget` deployment.

## Release contents

| Package | Version | Purpose |
| --- | --- | --- |
| XBullet.EasyMapping | 1.4.0 | Compiler generator, whole-source helpers, explicit value mappings, nested source/target paths, model approvals, and MSBuild integration |
| XBullet.EasyMapping.Abstractions | 1.4.0 | Mapping declaration attributes, whole-source helper bindings and consumed-source declarations, explicit values, and coverage options |

Both packages target .NET Standard 2.0 and use the [MIT license](../LICENSE).
The generator is built against Roslyn 4.8; compiler scenarios use C# 12. Samples,
the typed model fixtures, and the packed consumer are tested with the .NET 10 SDK.
Do not infer support for untested frameworks, IDE compiler hosts, trimming, or Native AOT.
See the [API guide](easy-mapping.md) and [changelog](../CHANGELOG.md).

## Version and metadata

`Directory.Build.props` is the version source for both packages. Central package
management reads the same value for the smoke consumer. Package inspection checks
the exact identities/versions, matching attributes dependency, author, MIT license,
README, repository URL/source commit, and required generator integration files.
Compiler DLLs and compiler package dependencies must not enter the runtime contract.

The analyzer release baseline is recorded under `AnalyzerReleases.Shipped.md` for
1.1.0. New diagnostic IDs after this release belong in `AnalyzerReleases.Unshipped.md`.

Both packages share one manually maintained Semantic Version in `Directory.Build.props`:

- Patch: compatible fixes, performance improvements, and maintenance, such as 1.1.2.
- Minor: new supported mapping capabilities or compatible public APIs, such as 1.2.0.
- Major: breaking changes to public APIs, mapping semantics, or supported compiler hosts.

Bump the working version after a release before producing changed package candidates.
Never move a published tag or reuse a published package version. The tag must match
the checked-in version exactly; publishing cannot override it. Release-note URLs use
that version rather than a separately maintained hard-coded tag.

Manual previews use a full checked-in version such as `1.2.0-preview.1`, a matching
`v1.2.0-preview.1` tag, and GitHub's pre-release option. Stable releases must not use
that option. The publishing gate verifies both cases. CI does not automatically bump
versions or publish preview packages. Candidate artifacts are consumed from a local
feed; publishing a preview and publishing a stable version are separate validations.

## Compatibility and coverage

The attributes project uses `PublicAPI.Shipped.txt` for the published API and
`PublicAPI.Unshipped.txt` for intentional additions. Review API changes explicitly;
move approved additions to the shipped file when releasing them. The public API
analyzer is private build tooling and is not a consumer dependency. The NuGet package
validator compares the attributes package with published **1.3.0** for binary
compatibility. Update the baseline to the latest stable version after publication.
It does not validate the analyzer-only generator's behavior: compiler, diagnostic,
generated-code, incremental, model-approval, and packed-consumer tests cover that.

The generator test project collects Coverlet coverage for the generator and attributes
assemblies using `tools/coverage.runsettings`. The gate requires 95% line and 80%
branch coverage for the generator, and 100% for the attributes. Initial Release
measurements were 96.99% lines / 85.26% branches and 100% respectively. Generated
consumer mappings and comparison libraries are outside these production-package
coverage totals. Coverage is a regression gate alongside semantic tests.

Local Windows validation for the 1.4.0 candidate passed all 536 generator tests
and 94 comparison tests. Release coverage measured 99.71% lines / 97.23% branches
for the generator and 100% lines / branches for the attributes. The final release
commit must also pass GitHub Actions validation on Windows and Ubuntu.

## Local validation

Run from the repository root with the .NET 10 SDK and PowerShell 7:

```powershell
dotnet restore XBullet.EasyMapping.slnx --locked-mode
dotnet restore tests/Diagnostics/Mapperly.UnmappedTarget/Mapperly.UnmappedTarget.csproj --locked-mode
dotnet restore tests/Diagnostics/Facet.InvalidInclude/Facet.InvalidInclude.csproj --locked-mode
dotnet build XBullet.EasyMapping.slnx --no-restore --configuration Release --verbosity minimal -m:1
dotnet test tests/MappingComparison.Tests/MappingComparison.Tests.csproj --no-restore --no-build --configuration Release --verbosity minimal
# Use a fresh results directory for every coverage run; the gate rejects stale/duplicate reports.
$results = "artifacts/test-results/coverage-$([guid]::NewGuid().ToString('N'))"
dotnet test tests/XBullet.EasyMapping.Generator.Tests/XBullet.EasyMapping.Generator.Tests.csproj --no-restore --no-build --configuration Release --verbosity minimal --collect "XPlat Code Coverage" --settings tools/coverage.runsettings --results-directory $results
./tools/Test-Coverage.ps1 -ResultsDirectory $results
./tools/Test-CiChecks.ps1
dotnet format XBullet.EasyMapping.slnx --no-restore --verify-no-changes --severity info --diagnostics IDE0011 IDE0055 IDE2003
dotnet format whitespace src/XBullet.EasyMapping.Generator/buildTransitive --folder --verify-no-changes
dotnet format whitespace tests/Diagnostics --folder --verify-no-changes
dotnet format whitespace tests/PackageSmoke --folder --verify-no-changes
./tools/Test-Documentation.ps1
./tools/Test-GeneratorDiagnostics.ps1
./tools/Export-GeneratedMappings.ps1 -Verify
./tools/Export-ModelApprovals.ps1 -Project samples/MappingComparison.EasyMapping/MappingComparison.EasyMapping.csproj -Configuration Release
./tools/Test-ModelApprovals.ps1
./tools/Test-Package.ps1
./tools/Test-Publishing.ps1
```

Each command must succeed. The two intentionally invalid fixture builds are checked
by `Test-GeneratorDiagnostics.ps1`, which accepts only their expected error IDs and
messages. Review changed snapshots; validation never approves model changes.

Packages are written to `artifacts/packages/1.4.0/`. The pack script verifies their
contents before restoring and executing a fresh consumer, then tests the received/
approved workflow. It never publishes anything.

## CI and release gates

The [validation workflow](../.github/workflows/validate.yml) runs for pushes, pull
requests, and manual dispatches. It uses pinned GitHub Actions commits, locked
restore, a Windows/Ubuntu matrix, read-only repository permissions, and a job timeout.
It builds and tests in Release, uploads TRX results and coverage even on failure,
and retains packages from the successful Windows
job. These artifacts are candidates until both matrix jobs pass for the same commit.

NuGet restore audits direct and transitive dependencies at moderate severity or
higher, with warnings treated as errors. [Dependency review](../.github/workflows/dependency-review.yml)
checks pull requests; [CodeQL](../.github/workflows/codeql.yml) analyzes C# on main,
pull requests, and weekly. [Dependabot](../.github/dependabot.yml) proposes weekly
NuGet and pinned Actions updates. Roslyn minor/major updates require deliberate
compiler-host compatibility review, so Dependabot does not propose them automatically.

Before publishing:

1. Review and commit the release changes; confirm the working tree is clean.
2. Push the release branch and wait for both validation jobs to pass. A local build
   does not establish a successful GitHub Actions run.
3. Verify the package version, changelog, MIT license, repository commit, and approval
   baselines on that exact commit. Use the verified CI package artifacts.
4. Confirm ownership of both NuGet package IDs and configure a publishing identity
   with permission to publish them. Never store API keys in source control.
5. Change the changelog's `Unreleased` label to the actual release date, commit it,
   and rerun CI before creating the `v1.4.0` tag on the final release commit.
6. Publish the GitHub Release for the tag, then review and approve the `nuget`
   deployment. The publishing workflow uploads the attributes package first,
   then the generator, and attaches the same verified packages to the release.
7. Restore a separate consumer from the published feed and verify the approved-file
   workflow once more. Update documentation to reflect actual publication.

Branch pushes, tags, GitHub Releases, and package publication are separate release
actions. The validation workflow has no publishing credentials or publish step.

## Trusted NuGet publication

The [publishing workflow](../.github/workflows/publish.yml) runs only when a GitHub
Release is published. A branch push or pull request does not publish packages.
Before creating a release, merge this workflow and its helpers into `main` and
wait for both validation jobs on the final commit to pass.

The NuGet trusted publisher is configured for:

| Setting | Value |
| --- | --- |
| Package owner / NuGet profile | `olgerd007` |
| GitHub repository | `olgerd007/XBullet.EasyMapping` |
| Workflow filename | `publish.yml` |
| Environment | `nuget` |
| Package pattern | `XBullet.EasyMapping*` |
| Scope | Push new packages and package versions |

The GitHub `nuget` environment holds the non-secret `NUGET_USER=olgerd007`
variable, allows release tags matching `v*`, and requires approval by `olgerd007`.
Self-approval is allowed so a sole maintainer can approve their own release.
Review the source commit and selected artifact before approving the deployment.
Environment approval is separate from the NuGet trusted publisher policy.

Verification resolves the tag, confirms its commit is on protected `main`, checks
that `v<version>` matches the tagged `Directory.Build.props`, and selects the
retained `release-packages` artifact from a successful main push validation for
that exact commit. Both named Windows/Ubuntu jobs must have actually succeeded.
The workflow checks the downloaded package versions, licenses, assets, and source
commit before requesting OIDC credentials. It publishes those CI packages without
rebuilding them. The temporary NuGet key is scoped to the publishing step's
environment and is not passed on the command line.

If CI artifacts expire, rerun the original main push validation before publishing.
Rerunning a partially completed publish accepts an existing NuGet version only
when its recorded source commit matches this release. Other versions are not
overwritten. Matching existing GitHub release assets are retained; mismatched
assets stop the workflow. NuGet indexing is checked after each package push for up
to 180 attempts, five seconds apart, with a 30-second timeout on each read. Publishing
has a 40-minute job limit. An indexing timeout after a successful push is distinct
from a rejected push: inspect the feed and source commit, then rerun the same release.
Do not create a new version merely to recover that timeout. This recovered the
initial 1.1.0 release, whose first run pushed both packages before timing out.

For a local dry run, download the validated CI packages and run:

```powershell
./tools/Publish-Packages.ps1 -PackageDirectory <downloaded-package-directory> -Version 1.4.0 -Commit <full-tagged-commit-sha> -WhatIf
```

The dry run validates the packages and reports the two planned pushes. It requests
no publishing credentials and does not contact NuGet or upload anything. Publishing
uses .NET SDK 10.0.300 or newer for `NUGET_API_KEY` environment support.

## Main branch policy

`main` is the default integration branch. The baseline policy is recorded in
[main.json](../.github/branch-protection/main.json):

- Changes go through pull requests, including administrator changes.
- Both `Validate (windows-latest)` and `Validate (ubuntu-latest)` must pass through
  GitHub Actions, and the branch must be up to date before merging.
- Review conversations must be resolved; submitted approvals become stale after
  new commits. A separate approving reviewer is optional for a sole maintainer.
- Force pushes and deletion of `main` are blocked.

The JSON records the intended settings; editing it does not automatically change
GitHub. A repository administrator must apply or update the protection through
GitHub settings or the branch-protection API. Preserve stronger existing policies.
The first push bootstraps an empty repository before protection is enabled.
