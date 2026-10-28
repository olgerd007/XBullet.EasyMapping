# Release 1.1.0

This document describes release preparation. It does not mean a tag, GitHub Release,
or NuGet publication exists. The `v1.1.0` branch is separate from a `v1.1.0` tag.

## Release contents

| Package | Version | Purpose |
| --- | --- | --- |
| XBullet.EasyMapping | 1.1.0 | Compiler generator, automatic approval discovery, MSBuild integration, and optional review helper |
| XBullet.EasyMapping.Abstractions | 1.1.0 | Mapping declaration attributes and coverage options |

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

## Local validation

Run from the repository root with the .NET 10 SDK and PowerShell 7:

```powershell
dotnet restore XBullet.EasyMapping.slnx --locked-mode
dotnet restore tests/Diagnostics/Mapperly.UnmappedTarget/Mapperly.UnmappedTarget.csproj --locked-mode
dotnet restore tests/Diagnostics/Facet.InvalidInclude/Facet.InvalidInclude.csproj --locked-mode
dotnet build XBullet.EasyMapping.slnx --no-restore --verbosity minimal -m:1
dotnet test tests/MappingComparison.Tests/MappingComparison.Tests.csproj --no-restore --no-build --verbosity minimal
dotnet test tests/XBullet.EasyMapping.Generator.Tests/XBullet.EasyMapping.Generator.Tests.csproj --no-restore --no-build --verbosity minimal
dotnet format XBullet.EasyMapping.slnx --no-restore --verify-no-changes
./tools/Test-Documentation.ps1
./tools/Test-GeneratorDiagnostics.ps1
./tools/Export-GeneratedMappings.ps1 -Verify
./tools/Export-ModelApprovals.ps1 -Project samples/MappingComparison.EasyMapping/MappingComparison.EasyMapping.csproj
./tools/Test-ModelApprovals.ps1
./tools/Test-Package.ps1
./tools/Test-Publishing.ps1
```

Each command must succeed. The two intentionally invalid fixture builds are checked
by `Test-GeneratorDiagnostics.ps1`, which accepts only their expected error IDs and
messages. Review changed snapshots; validation never approves model changes.

Packages are written to `artifacts/packages/1.1.0/`. The pack script verifies their
contents before restoring and executing a fresh consumer, then tests the received/
approved workflow. It never publishes anything.

## CI and release gates

The [validation workflow](../.github/workflows/validate.yml) runs for pushes, pull
requests, and manual dispatches. It uses pinned GitHub Actions commits, locked
restore, a Windows/Ubuntu matrix, read-only repository permissions, and a job timeout.
It uploads TRX results on failure and retains packages from the successful Windows
job. These artifacts are candidates until both matrix jobs pass for the same commit.

Before publishing:

1. Review and commit the release changes; confirm the working tree is clean.
2. Push the release branch and wait for both validation jobs to pass. A local build
   does not establish a successful GitHub Actions run.
3. Verify the package version, changelog, MIT license, repository commit, and approval
   baselines on that exact commit. Use the verified CI package artifacts.
4. Confirm ownership of both NuGet package IDs and configure a publishing identity
   with permission to publish them. Never store API keys in source control.
5. Change the changelog's `Unreleased` label to the actual release date, commit it,
   and rerun CI before creating the `v1.1.0` tag on the final release commit.
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
assets stop the workflow. NuGet indexing is checked after each package push.

For a local dry run, download the validated CI packages and run:

```powershell
./tools/Publish-Packages.ps1 -PackageDirectory <downloaded-package-directory> -Version 1.1.0 -Commit <full-tagged-commit-sha> -WhatIf
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
