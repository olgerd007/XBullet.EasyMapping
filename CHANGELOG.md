# Changelog

## 1.3.0 — 2026-10-30

- Select source converters explicitly with `MapProperty.Use`, including nullable
  values, nested source paths, and whole collections. Invalid helper bindings
  produce source-located configuration diagnostics.
- Supply target properties and constructor parameters with `MapConstant` and
  `MapGeneratedValue`. Generated-value helpers run once per configured target on
  each call; required-member initialization reuses the same value. Array constants
  create fresh arrays for each target and mapping call, including updates.
- Support dot-separated target paths for nested creation and existing-target
  updates, including nullable and struct intermediates. Validate conflicting,
  ignored, missing, and unwritable target paths at compile time.
- Include explicit helper names, constants, generated values, and nested target
  configurations in model-shape approvals. Existing approvals using these new
  configurations require review; builds never approve changes automatically.
- Expand regression coverage to 474 generator tests and 94 comparison tests,
  including runtime behavior, diagnostics, incremental updates, and approvals.
- Standardize handwritten C# formatting and enforce braces, multiline blocks, and
  blank-line separation in builds and CI, including standalone packaged helpers
  and test fixtures. Embedded compiler inputs and generated fixtures are exempt.

## 1.2.0 — 2026-10-29

- Support dot-separated source paths in `MapProperty` for flattening nested
  properties into target properties and constructor parameters, including updates.
  Nullable intermediate references and structs propagate null; leaf conversions
  retain the existing conversion and collection-copy rules.
- Validate each source path segment and count its root toward strict source
  coverage. Changing the configured path requires model-shape approval when enabled.
- Prefer constructors that cover non-ignored properties without public setters,
  including inherited properties and renamed source members. Keep parameterless
  construction preferred when it can cover the target.
- Attach diagnostics to the current source tree for IDE navigation, and include
  mapping names and actionable guidance in missing-source and conversion errors.
- Add regression coverage for repeated nested types, constructor selection,
  diagnostic locations, and nested source paths.
- Pin repository restores to nuget.org to avoid machine-level feed conflicts.

## 1.1.2 — 2026-10-29

- Qualify static and instance converter calls so mapping parameters cannot shadow them,
  including nullable conversions, collection elements, and existing-target updates.
- Reject file-local mapper classes with source-located EMAP001 instead of emitting
  an implementation that cannot complete the original declaration.
- Include enum underlying types in model approvals, including nested enums, so changes
  such as `int` to `long` require review even when enum values stay the same.
  Existing approvals containing enums require one reviewed refresh to add the
  `underlying` line. The v1 text format and approvals without enums are unchanged.
- Refresh the package README with published-feed installation and a complete example.

## 1.1.1 — 2026-10-28

- Remove identity LINQ projections when copying collection elements.
- Generate capacity-sized loops for converted lists from arrays and standard
  counted collection contracts, preserving nulls, converters and container isolation.
- Add collection regression tests and a frozen pre-optimization benchmark reference.
- Validate Release builds, public API approvals, and attributes package compatibility
  against published 1.1.0, with coverage gates for both production assemblies.
- Add NuGet auditing, dependency review, CodeQL, and weekly Dependabot updates.
- Retain manual shared package versioning and verify stable/pre-release tag consistency.
- Allow a longer NuGet indexing window and test recovery without republishing packages.

## 1.1.0 — 2026-10-28

- Add an owned incremental C# generator for typed DTO creation and existing-target updates.
- Support constructors, required/init members, inherited properties, nullable roots/members,
  collection materialization, declared nested mappings, and handwritten converters.
- Add strict source/target coverage and source-located compiler errors for unsupported,
  ambiguous, recursive, or unsafe mappings.
- Add opt-in model-shape approvals, automatically discovered `.mapper.approved.txt`
  baselines, and reviewable `.mapper.received.txt` candidates beside mapper sources.
- Add configurable approval locations and an optional PowerShell review helper.
- Verify 216 tests, including complex model graphs, deep-copy isolation, nullable/jagged
  collections, numeric boundaries, replacement updates, and nested approval invalidation.
- Report unsupported target indexers with EMAP009 instead of a misleading missing-source error.
- Package the generator and attributes separately with MIT licensing and repository metadata.
- Configure Windows/Ubuntu CI with documentation, package-content checks, test results,
  and verified package artifacts.

This is the first public source release of the owned generator. Earlier `0.1.0`
packages were local prototypes. Public namespace and package names remain unchanged.

EF projections, arbitrary cyclic graphs/reference tracking, presence-aware PATCH
semantics, and automatic dictionary/set/multidimensional-array mappings are outside
this release. Unsupported conversions require declared handwritten converters or
are rejected by the compiler; no runtime fallback is provided.
