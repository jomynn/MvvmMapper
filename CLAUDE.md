# CLAUDE.md — MvvmMapper

This file tells Claude Code how to work in this repository. Read it fully before making any change.

## What this project is

**MvvmMapper** is an offline static analysis CLI tool that scans WPF MVVM C# solutions and produces a relationship map:

```
View (.xaml)  →  ViewModel (.cs)  →  Service method  →  HTTP endpoint
```

The map handles real-world MVVM mess: shared ViewModels, Locator patterns, DI containers, Refit, RestSharp, and raw HttpClient. Output is JSON + Mermaid + a single self-contained HTML report.

## Hard rules

1. **Offline only.** No HTTP calls at runtime. No telemetry. No CDN links in generated HTML. Mermaid and any JS must be inlined.
2. **Read-only on user projects.** We analyze; we never modify the scanned solution.
3. **.NET 8 / C# 12.** Use primary constructors, collection expressions, file-scoped namespaces, records for all immutable types.
4. **Roslyn for `.cs`, `System.Xml.Linq` for `.xaml`.** No third-party XAML loaders.
5. **No `dynamic`, no reflection** outside the Roslyn API surface.
6. **All async methods take `CancellationToken`** as the last parameter.
7. **Confidence is mandatory on every edge.** Never emit an edge without a `Confidence` value and a human-readable `Reason` string.

## Architecture invariants

- `MvvmMapper.Core` is a pure library. It has zero dependency on `System.CommandLine` or any CLI concern.
- `MvvmMapper.Cli` is the only project allowed to call `Console.WriteLine` or `Environment.Exit`.
- All file I/O in `Core` goes through `IFileSystem` (abstraction over `System.IO.Abstractions` or a hand-rolled interface). Tests never touch the real disk.
- Logging is `ILogger<T>` everywhere in `Core`. CLI wires up `Microsoft.Extensions.Logging.Console`.
- Resolvers are independent. Adding a new resolver must not require editing existing ones. They register themselves with `GraphBuilder` via DI.
- The graph is immutable after `GraphBuilder.Build()` returns. Renderers receive a read-only view.

## Definitions

| Term | Meaning |
|---|---|
| **View** | A `.xaml` file that compiles to a `UserControl`, `Window`, or `Page` |
| **ViewModel** | A class whose name ends with a configured suffix (`ViewModel`, `VM`) OR inherits a configured base type |
| **Service** | A class injected into a ViewModel, identified by being a non-VM dependency in the VM's constructor |
| **Endpoint** | An HTTP route + verb pair, e.g. `POST /api/auth/login` |
| **Edge** | A directed relationship between two nodes, with a kind, confidence, and reason |
| **Shared ViewModel** | A VM with `BindsTo` edges from 2 or more distinct Views — flagged by analysis |

## Resolver priority (do not reorder without discussion)

When resolving View → ViewModel:

1. Explicit XAML (`<UserControl.DataContext>` element) — **High**
2. Code-behind (`this.DataContext = ...` in `.xaml.cs`) — **High**
3. Locator binding (`{Binding X, Source={StaticResource Locator}}`) — **High** if Locator class is resolved
4. DI container registration — **Medium**
5. Naming convention (`FooView` ↔ `FooViewModel`) — **Low**

All matching resolvers produce edges. The UI/report can filter by confidence; we do not silently drop low-confidence edges.

## Folder structure

```
src/
  MvvmMapper.Cli/         CLI entry, dotnet global tool
  MvvmMapper.Core/        all analysis logic
  MvvmMapper.Core.Templates/  embedded HTML report template
tests/
  MvvmMapper.Core.Tests/
  MvvmMapper.Cli.Tests/
samples/
  simple-mvvm/, locator-pattern/, di-pattern/, shared-vm/
.claude/
  skills/
    mvvm-scanning/SKILL.md
```

## Conventions

- **Namespaces** match folder paths under `src/`.
- **One public type per file.** Filename matches type name exactly.
- **Records over classes** for any type that is data + no behavior.
- **`sealed` by default** on classes unless inheritance is explicitly intended.
- **`internal`** is the default visibility; only promote to `public` what the CLI or tests genuinely consume.
- **Test naming:** `MethodName_Scenario_ExpectedBehavior` (e.g. `Resolve_ExplicitXamlDataContext_EmitsHighConfidenceEdge`).
- **Commit messages:** Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`).

## What good output looks like

A successful scan of `samples/shared-vm`:

- 3 `ViewNode`s (LoginView, RegisterView, ForgotPasswordView)
- 1 `ViewModelNode` (AuthViewModel) — flagged as shared (fan-in = 3)
- 3 `BindsTo` edges from each View to AuthViewModel
- 2 `EndpointNode`s (`POST /api/auth/login`, `POST /api/auth/register`)
- HTML report's "By Endpoint" tab shows `POST /api/auth/login` reached from 3 Views

If the scan produces anything different on that sample, the build is broken.

## What NOT to do

- Do **not** rewrite resolvers to share state. Each resolver is a pure function over the parsed model.
- Do **not** add a database or persistent cache. In-memory only.
- Do **not** introduce a web server or REST API. The tool runs, writes files, exits.
- Do **not** swallow exceptions in resolvers — log them with the file path and continue, so one bad file doesn't kill the scan.
- Do **not** use `Console.WriteLine` in `Core`. Use `ILogger`.
- Do **not** put any code in `Program.cs` beyond bootstrap and `System.CommandLine` wiring.

## When you're unsure

Stop and ask. Specifically ask when:

- A user's WPF project uses a pattern (e.g. Caliburn.Micro conventions, Stylet) not covered by an existing resolver.
- The spec in `01-master-prompt.md` conflicts with what you're seeing in the code.
- A library upgrade would break the API surface.

For minor judgment calls (variable naming, internal helper extraction), use your taste and keep moving.

## Skill files

When the task at hand is "scan a WPF project and resolve MVVM relationships," consult `.claude/skills/mvvm-scanning/SKILL.md`. It contains the encoded knowledge of how WPF MVVM apps are actually structured in the wild — Locator patterns, DataTemplate tricks, navigation frameworks, and so on. Read it before writing resolver logic.
