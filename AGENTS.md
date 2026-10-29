# Repository Development Instructions

These instructions apply to development work throughout this repository.
Follow explicit user requests and more specific directory instructions first.

## Engineering Priorities

- Prioritize correctness, readability, maintainability, testability, and security.
- Respect existing architecture and public behavior unless a change is requested.
- Make the smallest complete change that satisfies the requirement.
- Prefer composition, explicit dependencies, strong typing, and cohesive classes.
- Avoid speculative abstractions, unrelated refactoring, and unnecessary packages.
- Apply domain-driven design where the business model benefits from it.

## Clean Architecture

Keep business rules independent of frameworks and technical implementations.
Dependencies must point inward toward domain rules and application use cases.

When separate application layers are justified, use these boundaries:

```text
Api -> Application
Api -> Infrastructure (dependency injection composition only)
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Domain -> no other project
```

- Domain contains entities, value objects, invariants, domain services, and events.
  It must not depend on persistence, HTTP, logging, messaging, or other infrastructure.
- Application contains use cases, validation, result models, and abstractions for
  external dependencies. It must not reference concrete infrastructure implementations.
- Infrastructure implements application or domain abstractions for persistence,
  external services, storage, messaging, and other technical concerns.
- API handles transport, authentication, authorization, and composition. Keep
  endpoints thin and expose contracts rather than persistence or domain entities.
- Protect domain invariants through explicit behavior; avoid unrestricted setters
  where state changes require validation.
- Organize application code by feature or use case when useful.
- Keep infrastructure-specific types out of domain and application contracts.

For a new application, prefer this structure when its scope warrants these layers:

```text
src/
  <Project>.Domain/
  <Project>.Application/
  <Project>.Infrastructure/
  <Project>.Api/
tests/
  <Project>.Domain.Tests/
  <Project>.Application.Tests/
  <Project>.Infrastructure.IntegrationTests/
  <Project>.Api.IntegrationTests/
```

For libraries and smaller projects, preserve appropriate boundaries within the
existing structure. Do not create application layers or extra projects merely to
organize folders.

## SOLID Principles

- **Single responsibility:** Give each class one cohesive responsibility and one
  primary reason to change. Keep business rules out of transport and persistence code.
- **Open/closed:** Use strategies or policies when multiple behaviors genuinely
  exist or extension is required. Do not replace simple conditionals mechanically.
- **Liskov substitution:** Implementations must honor their contracts without
  stronger preconditions, weaker guarantees, or unsupported normal operations.
- **Interface segregation:** Keep interfaces focused on their consumers' needs.
  Split unrelated responsibilities instead of creating broad service interfaces.
- **Dependency inversion:** Depend on abstractions at architectural boundaries.
  Use constructor injection and layer-specific registration. Avoid static service
  locators, hidden dependencies, and interfaces added without a clear purpose.

## C# Code Style

Use [Microsoft's C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
as the baseline, with the following repository rules taking precedence:

- Use four spaces for indentation; do not use tabs.
- Use Allman braces: place opening and closing braces on separate lines, aligned
  with the construct that owns the block.
- Always use braces for control-flow bodies, even for a single statement.
- Put each statement and declaration on its own line. Expand compact control-flow
  blocks such as `if (condition) { DoWork(); }` across multiple lines.
- Put a space after control-flow keywords, for example `if (condition)` and
  `foreach (var item in items)`.
- Separate logical steps with one blank line. After a control-flow block, add a
  blank line before the next independent statement at the same scope level.
- Keep related `else`, `catch`, and `finally` clauses on the next line after the
  preceding closing brace, without an intervening blank line. Do not insert blank
  lines between consecutive closing braces or between every statement.
- Keep at least one blank line between method and property definitions.
- Treat `.editorconfig` as the executable formatting configuration. The blank-line
  rule uses Microsoft's experimental IDE2003 option; keep the written convention
  even in tooling that does not support it.
- Apply these conventions to new and modified code. Keep repository-wide
  reformatting separate from functional changes unless explicitly requested.
- Enforce IDE0011, IDE0055, and IDE2003 as warnings. Builds treat warnings as errors.
  Verify the agreed rules with
  `dotnet format --no-restore --verify-no-changes --severity info --diagnostics IDE0011 IDE0055 IDE2003`.
- Also verify whitespace in the packaged build helper, standalone diagnostic
  fixtures, and package smoke consumer; these are outside the solution.
- Generated verification fixtures under `artifacts/`, build output, and embedded
  compiler-test source strings are exempt from handwritten-code formatting rules.

## .NET Implementation Standards

- Preserve the repository's target framework and conventions. Enable nullable
  reference types for new projects and use supported C# features for clarity.
- Prefer immutable contracts, meaningful names, guard clauses, and small methods.
- Put each top-level C# type in a separate file named after that type, including
  classes, records, structs, interfaces, enums, and delegates. Keep `Program.cs`
  focused on startup or top-level statements. When modifying a file containing
  multiple top-level types, split them without changing namespaces, accessibility,
  or behavior. Nested private implementation types may stay with their owning
  type; generated files and inline compiler-test source snippets are exempt.
- Separate input validation from domain invariants and enforce both at the
  appropriate boundary.
- Model expected failures clearly. Handle unexpected failures at the application
  boundary; do not swallow exceptions or expose internal exception details.
- Use asynchronous I/O and propagate `CancellationToken` where meaningful.
  Do not block asynchronous work with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Use `TimeProvider` for domain or application time-dependent behavior.
- Use strongly typed configuration, validate options where practical, and keep
  secrets outside source control.
- Use structured logging without secrets, tokens, or sensitive request contents.
- Use `IHttpClientFactory` for HTTP integrations, with reasonable timeouts and
  retries limited to operations that can safely be repeated.
- Keep EF Core in Infrastructure. Use bounded queries, read-only no-tracking
  queries where appropriate, and transactions for atomic changes.
- Add aggregate repositories only when they provide a meaningful boundary; do not
  introduce generic repositories by default or expose `IQueryable` across layers.
- Make message consumers idempotent and acknowledge messages only after required
  processing succeeds. Consider inbox/outbox patterns when consistency requires them.

## Git Branch Naming

- Name new branches by the purpose of the work: `feature/<description>` for
  features, `bugfix/<description>` for bug fixes, `hotfix/<description>` for
  urgent fixes, `refactor/<description>` for refactoring, `docs/<description>`
  for documentation, `test/<description>` for tests, and `chore/<description>`
  for maintenance.
- Use short, descriptive, lowercase names with hyphens between words, such as
  `feature/nested-mapping` or `bugfix/nullable-address`.
- Do not use the `codex/` prefix. Follow an explicitly requested branch name
  when the user provides one.

## Development Workflow

1. Inspect the existing code, instructions, tests, and working-tree changes.
2. Give a brief plan covering affected files, design choices, tests, and risks.
3. Implement a focused change that preserves public behavior unless requested otherwise.
4. Add meaningful tests for business-critical behavior and regression risks.
   Significant use cases must be testable without starting the complete application.
5. Run applicable validation from the solution directory:

   ```text
   dotnet restore
   dotnet build --no-restore --verbosity minimal
   dotnet test <test-project> --no-restore --verbosity minimal
   dotnet format --no-restore --verify-no-changes --severity info --diagnostics IDE0011 IDE0055 IDE2003
   dotnet format whitespace src/XBullet.EasyMapping.Generator/buildTransitive --folder --verify-no-changes
   dotnet format whitespace tests/Diagnostics --folder --verify-no-changes
   dotnet format whitespace tests/PackageSmoke --folder --verify-no-changes
   ```

   Run tests for each relevant test project. Run formatting verification when
   configured. For documentation-only changes, inspect the diff and whitespace;
   do not run unrelated builds or tests. Report unavailable tooling or projects.
6. Review dependency direction, validation, cancellation, failure handling,
   security, query bounds, service lifetimes, test coverage, and unnecessary complexity.
7. Report results using Summary, Architectural Decisions, Changed Files,
   Validation, Assumptions, and Remaining Concerns. Only claim checks passed
   when they actually ran successfully.

Test domain rules and invariants in domain tests, use-case behavior against
abstractions in application tests, and framework or external integration behavior
in integration tests. Explain breaking changes and new dependencies.
