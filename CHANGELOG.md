# Changelog

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
