# Claude Code Master Prompt — MVVM Mapper

> Paste this entire file as your first message in a new Claude Code session.
> Place `CLAUDE.md` and `.claude/skills/mvvm-scanning/SKILL.md` in the repo root **before** starting.

---

## ROLE

You are a senior .NET architect building **MvvmMapper**, an offline static analysis CLI tool that scans WPF MVVM C# solutions and generates a code map showing the relationship:

```
View (XAML)  →  ViewModel (C#)  →  Service Method  →  HTTP Endpoint
```

The tool must handle real-world complexity: **shared ViewModels** (one VM bound by multiple Views), Locator patterns (MVVM Light / CommunityToolkit.Mvvm), DI containers, Refit, RestSharp, and raw HttpClient.

---

## NON-NEGOTIABLE CONSTRAINTS

1. **100% offline** — no network calls at runtime, no telemetry, no cloud APIs
2. **.NET 8 minimum** — use modern C# 12 features (primary constructors, collection expressions, records)
3. **Roslyn-based** — `Microsoft.CodeAnalysis.CSharp.Workspaces` for `.cs`, `System.Xml.Linq` for `.xaml`
4. **Distributable as a dotnet global tool** — `dotnet tool install -g MvvmMapper`
5. **Self-contained HTML output** — single file, Mermaid inlined, no CDN dependencies
6. **Confidence-scored edges** — every relationship must carry High/Medium/Low confidence
7. **No code generation or modification of user projects** — read-only analysis

---

## TECH STACK (mandatory)

| Concern | Library | Version |
|---|---|---|
| C# parsing | `Microsoft.CodeAnalysis.CSharp.Workspaces` | 4.9+ |
| MSBuild integration | `Microsoft.CodeAnalysis.Workspaces.MSBuild` | 4.9+ |
| MSBuild locator | `Microsoft.Build.Locator` | 1.7+ |
| XAML parsing | `System.Xml.Linq` (BCL) | — |
| CLI parsing | `System.CommandLine` | 2.0-beta4+ |
| Logging | `Microsoft.Extensions.Logging.Console` | 8.0+ |
| JSON | `System.Text.Json` (BCL) | — |
| Testing | `xUnit` + `FluentAssertions` | latest |

Do **not** add other dependencies without justifying the trade-off.

---

## SOLUTION LAYOUT (create exactly this)

```
MvvmMapper/
├── MvvmMapper.sln
├── README.md
├── CLAUDE.md                        ← already provided
├── .claude/skills/mvvm-scanning/SKILL.md   ← already provided
├── src/
│   ├── MvvmMapper.Cli/              dotnet tool entry point
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   │   ├── ScanCommand.cs
│   │   │   └── InitCommand.cs       (writes default mvvm-map.json)
│   │   └── MvvmMapper.Cli.csproj    <PackAsTool>true</PackAsTool>
│   │
│   ├── MvvmMapper.Core/             library (the brain)
│   │   ├── Configuration/
│   │   │   ├── MvvmMapConfig.cs
│   │   │   └── ConfigLoader.cs
│   │   ├── Discovery/
│   │   │   ├── SolutionLoader.cs
│   │   │   └── FileDiscoverer.cs
│   │   ├── Parsing/
│   │   │   ├── XamlParser.cs
│   │   │   ├── XamlDocument.cs      parsed model
│   │   │   └── RoslynContext.cs     wraps Compilation + SemanticModel cache
│   │   ├── Resolvers/
│   │   │   ├── IResolver.cs
│   │   │   ├── ViewToViewModel/
│   │   │   │   ├── ExplicitXamlResolver.cs
│   │   │   │   ├── LocatorResolver.cs
│   │   │   │   ├── CodeBehindResolver.cs
│   │   │   │   ├── DiContainerResolver.cs
│   │   │   │   └── NamingConventionResolver.cs
│   │   │   ├── Commands/
│   │   │   │   └── CommandResolver.cs
│   │   │   └── Endpoints/
│   │   │       ├── HttpClientResolver.cs
│   │   │       ├── RefitResolver.cs
│   │   │       └── RestSharpResolver.cs
│   │   ├── Graph/
│   │   │   ├── Node.cs (abstract) + ViewNode, ViewModelNode, MethodNode, ServiceNode, EndpointNode
│   │   │   ├── Edge.cs + EdgeKind enum + Confidence enum
│   │   │   ├── MvvmGraph.cs         the in-memory graph store
│   │   │   └── GraphBuilder.cs      orchestrates all resolvers
│   │   ├── Analysis/
│   │   │   ├── OrphanDetector.cs
│   │   │   ├── FanOutAnalyzer.cs    shared VM detection
│   │   │   └── EndpointImpactAnalyzer.cs
│   │   ├── Rendering/
│   │   │   ├── IRenderer.cs
│   │   │   ├── JsonRenderer.cs
│   │   │   ├── MermaidRenderer.cs
│   │   │   └── HtmlRenderer.cs      single-file interactive report
│   │   └── MvvmMapper.Core.csproj
│   │
│   └── MvvmMapper.Core.Templates/   embedded HTML/CSS/JS templates
│       └── report.template.html
│
├── tests/
│   ├── MvvmMapper.Core.Tests/
│   │   ├── Resolvers/ (one test class per resolver)
│   │   ├── Graph/
│   │   └── Fixtures/ (small sample WPF solutions for each pattern)
│   └── MvvmMapper.Cli.Tests/
│
└── samples/
    ├── simple-mvvm/                 5-View toy solution (naming convention)
    ├── locator-pattern/             MVVM Light style with ViewModelLocator
    ├── di-pattern/                  Microsoft.Extensions.DependencyInjection
    └── shared-vm/                   3 Views binding the same ViewModel
```

---

## CORE DATA MODEL (define these first, before any logic)

```csharp
public enum NodeKind { View, ViewModel, Method, Service, Endpoint }
public enum EdgeKind { BindsTo, Invokes, Contains, Calls, Hits, ComposedOf, Implements }
public enum Confidence { High, Medium, Low }

public abstract record Node(string Id, string DisplayName, NodeKind Kind, string SourceFile, int? SourceLine);

public record ViewNode(...)        : Node;
public record ViewModelNode(...)   : Node;   // includes FullyQualifiedName
public record MethodNode(...)      : Node;   // includes OwningType
public record ServiceNode(...)     : Node;
public record EndpointNode(string Verb, string Route, ...) : Node;

public record Edge(string FromId, string ToId, EdgeKind Kind, Confidence Confidence, string Reason);
```

`MvvmGraph` exposes:
- `IReadOnlyDictionary<string, Node> Nodes`
- `IReadOnlyList<Edge> Edges`
- `IEnumerable<Edge> EdgesFrom(string nodeId)` / `EdgesTo(string nodeId)`
- `IEnumerable<EndpointNode> ReachableEndpoints(string viewId)` — graph traversal

---

## BUILD ORDER (do not skip phases)

Implement and commit each phase before starting the next. After every phase, run tests and confirm the CLI runs end-to-end on the `samples/simple-mvvm` solution.

### Phase 1 — Skeleton & Discovery (deliverable: CLI prints discovered files)
- Solution structure, csproj files, `MvvmMapper.sln`
- `Program.cs` with `System.CommandLine` parsing `scan <path>`
- `SolutionLoader` using `MSBuildLocator.RegisterDefaults()` then `MSBuildWorkspace`
- `FileDiscoverer` returns `(XamlFiles, CsFiles)`
- Output: JSON dump of discovered files

### Phase 2 — Parsing
- `XamlParser` extracts: `DataContext` declarations, `Command` bindings, `DataTemplate` `DataType` attributes, child UserControl references
- `RoslynContext` caches `Compilation` and per-tree `SemanticModel` (lazy, thread-safe)
- Identifies ViewModel classes: suffix `ViewModel` OR inherits from configured base types

### Phase 3 — View ↔ ViewModel resolvers
Run resolvers in this priority order; first match wins, but **all** matches are recorded with their confidence:
1. `ExplicitXamlResolver` — `<UserControl.DataContext><vm:Foo /></...>` → **High**
2. `CodeBehindResolver` — `this.DataContext = new FooVM()` in `.xaml.cs` → **High**
3. `LocatorResolver` — `{Binding Foo, Source={StaticResource Locator}}` → resolve Locator property type → **High**
4. `DiContainerResolver` — Views registered with VM injected via constructor → **Medium**
5. `NamingConventionResolver` — `FooView.xaml` ↔ `FooViewModel.cs` → **Low**

### Phase 4 — Command & Method resolvers
- Find `ICommand` properties in VMs, trace to Execute methods (RelayCommand, DelegateCommand, AsyncCommand patterns)
- Match XAML `Command="{Binding XCommand}"` to the resolved Execute method

### Phase 5 — Endpoint resolvers
- `HttpClientResolver` — detect `HttpClient.GetAsync/PostAsync/...`, `SendAsync(new HttpRequestMessage(...))`. Extract string literals; mark as **Medium** if URL is composed from variables.
- `RefitResolver` — interfaces with `[Get("/...")]`, `[Post("/...")]`, etc. attributes → **High**
- `RestSharpResolver` — `new RestRequest("...", Method.GET)` → **High**

### Phase 6 — Rendering
- `JsonRenderer` — full graph dump (machine-readable)
- `MermaidRenderer` — three pivots: by-view, by-vm, by-endpoint
- `HtmlRenderer` — single self-contained HTML file with tabs + search + filter by confidence

### Phase 7 — Analysis & polish
- Orphan detection (Views without VMs, VMs without Views, unreachable endpoints)
- Fan-out analyzer (highlight VMs bound by 3+ Views)
- `--watch` mode using `FileSystemWatcher`
- `mvvm-map init` writes default `mvvm-map.json` to current directory

---

## CLI SURFACE (exact)

```
mvvm-map scan <path>                       (.sln, .csproj, or folder)
  --output <dir>             default: ./mvvm-map-output
  --format <html|md|json|all>  default: all
  --pivot <view|vm|endpoint|all>  default: all
  --confidence <high|medium|low>  minimum to include
  --filter-namespace <pattern>   e.g. "MyApp.*"
  --exclude <glob>               repeatable
  --config <file>                default: ./mvvm-map.json
  --watch                        rescan on change
  --verbose

mvvm-map init                              writes default mvvm-map.json
mvvm-map --version
```

---

## CONFIGURATION FILE SCHEMA

`mvvm-map.json` (project root):

```json
{
  "patterns": {
    "viewSuffix": ["View", "Page", "Window"],
    "viewModelSuffix": ["ViewModel", "VM"],
    "viewModelBaseTypes": ["ObservableObject", "ViewModelBase", "BindableBase", "ReactiveObject"]
  },
  "diContainers": ["Microsoft.Extensions.DependencyInjection", "Autofac", "SimpleInjector"],
  "locatorClasses": ["ViewModelLocator"],
  "httpClientTypes": ["HttpClient", "IRestClient", "IFlurlClient"],
  "endpointBaseUrls": {
    "default": "https://api.example.com",
    "Auth": "https://auth.example.com"
  },
  "exclude": ["**/Generated/**", "**/obj/**", "**/bin/**"]
}
```

---

## TESTING REQUIREMENTS

- Every resolver gets a dedicated test fixture under `tests/.../Fixtures/<ResolverName>/` containing minimal XAML + C# files demonstrating both the positive case and at least one tricky negative case.
- Use FluentAssertions style: `graph.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.BindsTo && e.Confidence == Confidence.High);`
- Minimum coverage target: **80% line coverage on `MvvmMapper.Core`**.
- One integration test per `samples/*` folder — scans the sample, asserts expected node and edge counts.

---

## QUALITY BAR

- All public APIs in `Core` have XML doc comments
- No `dynamic`, no reflection except where Roslyn forces it
- All `async` methods take `CancellationToken`
- All file I/O goes through an injectable `IFileSystem` abstraction so tests don't touch disk
- Logging via `ILogger<T>` — never `Console.WriteLine` outside `Program.cs`

---

## DELIVERABLES CHECKLIST

When you believe the project is complete, confirm each item:

- [ ] `dotnet build` succeeds with zero warnings
- [ ] `dotnet test` — all tests pass, ≥80% coverage on Core
- [ ] `dotnet pack src/MvvmMapper.Cli` produces a `.nupkg`
- [ ] `dotnet tool install --global --add-source ./nupkg MvvmMapper` works
- [ ] `mvvm-map scan samples/shared-vm` produces HTML showing 3 Views correctly mapped to 1 shared VM
- [ ] HTML report opens offline, all assets inlined, search works, Mermaid renders
- [ ] `README.md` includes: install, quick start, screenshot of HTML report, config reference, how each resolver works

---

## WORKING PROTOCOL

1. **Read `CLAUDE.md` and `.claude/skills/mvvm-scanning/SKILL.md` first**, before writing any code.
2. Start at Phase 1. After each phase, summarize what you built, run the tests, and **wait for my confirmation** before moving to the next phase.
3. If a design decision isn't covered above, propose 2–3 options with trade-offs rather than picking silently.
4. If you discover the codebase needs to deviate from this spec (e.g. a library doesn't behave as expected), flag it explicitly and propose the deviation.

Begin Phase 1.
