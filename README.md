# MvvmMapper

> Offline static analysis for WPF MVVM C# solutions. Maps `View → ViewModel → Service → HTTP Endpoint`.

MvvmMapper scans a WPF MVVM solution without running or compiling it and produces an interactive HTML report, machine-readable JSON, and Mermaid diagrams — all self-contained and offline.

## Install

```sh
dotnet tool install -g MvvmMapper --add-source ./nupkg
```

## Quick Start

```sh
# Scan a solution folder
mvvm-map scan ./MyWpfSolution --output ./mvvm-map-output

# Scan a specific .sln file
mvvm-map scan MyApp.sln --output ./mvvm-map-output --format html

# Scan a single project
mvvm-map scan MyApp.csproj --output ./mvvm-map-output

# Emit only JSON (for CI pipelines)
mvvm-map scan ./MyWpfSolution --format json

# Watch for source changes and re-scan automatically
mvvm-map scan ./MyWpfSolution --watch

# Write a default config file to the current directory
mvvm-map init
```

## CLI Reference

### `mvvm-map scan <path>`

Scans a WPF MVVM solution and generates a relationship map.

| Argument / Option | Default | Description |
|---|---|---|
| `path` | *(required)* | Path to a `.sln`, `.csproj`, or folder to scan |
| `--output` | `./mvvm-map-output` | Directory where output files are written |
| `--format` | `all` | Output format: `html`, `mermaid`, `json`, or `all` (pipe-separated for multiple: `html\|json`) |
| `--confidence` | `low` | Minimum confidence level to include in output: `high`, `medium`, or `low` |
| `--config` | *(auto-detected)* | Path to a `mvvm-map.json` config file |
| `--verbose` | `false` | Enable verbose (debug-level) logging |
| `--watch` | `false` | Re-scan whenever a `.cs` or `.xaml` file changes (Ctrl+C to stop) |

### `mvvm-map init`

Writes a default `mvvm-map.json` configuration file to the current directory. Skips if the file already exists.

## Output Files

| File | Description |
|---|---|
| `report.html` | Self-contained interactive report — tabs, search, confidence filter. No CDN links; fully offline. |
| `graph.json` | Full machine-readable graph (nodes + edges with confidence and reasons) |
| `mermaid-by-view.md` | Mermaid flowchart per View showing bound ViewModel and reachable endpoints |
| `mermaid-by-vm.md` | Mermaid flowchart per ViewModel showing bound Views and reachable endpoints. Shared VMs are marked `[SHARED]`. |
| `mermaid-by-endpoint.md` | Mermaid flowchart per HTTP endpoint showing which Views can reach it |

## Configuration (`mvvm-map.json`)

Run `mvvm-map init` to generate a default file, then edit to match your project.

```json
{
  "patterns": {
    "viewSuffix": ["View", "Page", "Window"],
    "viewModelSuffix": ["ViewModel", "VM"],
    "viewModelBaseTypes": ["ObservableObject", "ViewModelBase", "BindableBase", "ReactiveObject"]
  },
  "diContainers": [
    "Microsoft.Extensions.DependencyInjection",
    "Autofac",
    "SimpleInjector"
  ],
  "locatorClasses": ["ViewModelLocator"],
  "httpClientTypes": ["HttpClient", "IRestClient", "IFlurlClient"],
  "endpointBaseUrls": {
    "default": "https://api.example.com"
  },
  "exclude": ["**/Generated/**", "**/obj/**", "**/bin/**"]
}
```

| Field | Purpose |
|---|---|
| `patterns.viewSuffix` | Suffixes that identify a XAML file as a View (used by naming-convention resolver) |
| `patterns.viewModelSuffix` | Suffixes that identify a class as a ViewModel |
| `patterns.viewModelBaseTypes` | Base class names that also classify a class as a ViewModel |
| `diContainers` | DI frameworks to detect registration calls from |
| `locatorClasses` | Class names to parse as ViewModelLocator implementations |
| `httpClientTypes` | HttpClient-like type names to scan for `GetAsync`/`PostAsync`/etc. |
| `endpointBaseUrls` | Base URL hints for resolving relative routes |
| `exclude` | Glob patterns for paths to skip during discovery |

## How Each Resolver Works

### View → ViewModel Resolvers

Resolvers are applied in priority order. All matching resolvers produce edges; higher-confidence edges are preferred but lower-confidence edges are never silently dropped.

#### 1. ExplicitXamlResolver (High confidence)

Detects an explicit `DataContext` element in XAML:

```xml
<UserControl.DataContext>
    <vm:LoginViewModel />
</UserControl.DataContext>
```

#### 2. CodeBehindResolver (High confidence)

Detects `DataContext` assignment in `.xaml.cs` code-behind files:

```csharp
// Object creation
this.DataContext = new LoginViewModel();

// Constructor injection
public LoginView(LoginViewModel vm)
{
    DataContext = vm;
}
```

#### 3. LocatorResolver (High / Low confidence)

Detects MVVM Light / CommunityToolkit.Mvvm Locator patterns:

```xml
DataContext="{Binding LoginVM, Source={StaticResource Locator}}"
```

Parses the configured `locatorClasses` (default: `ViewModelLocator`) to resolve the property type. Emits **High** confidence if the Locator property is found, **Low** if the property cannot be resolved.

#### 4. DiContainerResolver (Medium confidence)

Finds DI registration calls in bootstrap files (`App.xaml.cs`, `Startup.cs`, `Program.cs`, `Bootstrapper.cs`):

```csharp
services.AddSingleton<LoginView>();
services.AddSingleton<LoginViewModel>();
```

Then checks if the View's constructor accepts the ViewModel as a parameter.

#### 5. NamingConventionResolver (Low confidence)

Matches files by stripping configured suffixes:

```
LoginView.xaml  ↔  LoginViewModel.cs   (strips "View" / "ViewModel")
MainPage.xaml   ↔  MainPageVM.cs       (strips "Page" / "VM")
```

### Command Resolver

**CommandResolver** traces `ICommand` properties to their `Execute` delegates:

```csharp
// RelayCommand constructor binding
public ICommand LoginCommand { get; } = new RelayCommand(ExecuteLogin);

// CommunityToolkit.Mvvm source generator attribute
[RelayCommand]
private void ExecuteLogin() { ... }
```

Emits `Invokes` edges from the ViewModel to the target method.

### Endpoint Resolvers

#### HttpClientResolver

Scans `HttpClient` calls for route strings:

```csharp
await _http.PostAsync("/api/auth/login", content);      // High confidence (literal)
await _http.GetAsync($"/api/users/{userId}");           // Medium confidence (interpolated)
```

#### RefitResolver

Reads `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Patch]` attributes on Refit interface methods:

```csharp
public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest req);
}
```

Emits **High** confidence edges from the interface method to the endpoint. Also traces callers of those interface methods to emit `Calls` edges.

#### RestSharpResolver

Detects `RestRequest` construction with literal route strings:

```csharp
var request = new RestRequest("/api/users", Method.Get);
```

Emits **High** confidence edges.

## Analysis

MvvmMapper's analysis engine reports:

| Finding | Description |
|---|---|
| **Orphaned Views** | Views with no ViewModel binding (no outbound `BindsTo` edge) |
| **Orphaned ViewModels** | ViewModels not bound by any View (no inbound `BindsTo` edge) |
| **Unreachable Endpoints** | HTTP endpoints that no method reaches (no inbound `Hits` edge) |
| **Shared ViewModels** | ViewModels with 2+ Views binding to them (fan-in ≥ 2) — flagged in the report and Mermaid diagrams |
| **Endpoint Impact** | Which Views can transitively reach each HTTP endpoint (forward BFS through the graph) |

Findings appear in the HTML report's analysis banner and in the console output after each scan.

## Samples

| Sample | Pattern Demonstrated |
|---|---|
| `samples/simple-mvvm` | Naming-convention resolver: `FooView.xaml` ↔ `FooViewModel.cs` |
| `samples/shared-vm` | Explicit DataContext, one ViewModel (`AuthViewModel`) shared by 3 Views with fan-in flag |

Run a sample scan:

```sh
mvvm-map scan samples/shared-vm --output /tmp/shared-vm-out
```

Expected output: 3 ViewNodes, 1 ViewModelNode (AuthViewModel, flagged as shared, fan-in=3), and 3 `BindsTo` edges.

## Architecture

```
MvvmMapper.Core             Pure library — all analysis logic, no CLI concerns
  Configuration/            Config loading (mvvm-map.json)
  Discovery/                File discovery (glob-based, with optional MSBuild workspace)
  Graph/                    Immutable MvvmGraph, node/edge types, GraphBuilder
  Parsing/                  XamlParser (System.Xml.Linq), RoslynContext
  Resolvers/                One resolver per MVVM pattern, registered via DI
    ViewToViewModel/        ExplicitXaml, CodeBehind, Locator, DiContainer, NamingConvention
    Commands/               RelayCommand / [RelayCommand] attribute tracing
    Endpoints/              HttpClient, Refit, RestSharp
  Rendering/                Html, Mermaid, Json renderers
  Analysis/                 OrphanDetector, FanOutAnalyzer, EndpointImpactAnalyzer, AnalysisRunner

MvvmMapper.Cli              dotnet global tool entry point
  Commands/                 ScanCommand, InitCommand (System.CommandLine wiring only)
  Program.cs                Bootstrap, logging setup — nothing else

MvvmMapper.Core.Tests       Unit and integration tests (xUnit, FluentAssertions)
```

Key invariants:
- `MvvmMapper.Core` has zero dependency on `System.CommandLine` or any CLI concern.
- All file I/O in `Core` goes through `IFileSystem` — tests use `FakeFileSystem`, never the real disk.
- The graph is **immutable** after `GraphBuilder.Build()` returns.
- Every edge carries a `Confidence` level (`High`, `Medium`, `Low`) and a human-readable `Reason` string.
- All resolvers are independent — adding a new one does not require editing existing ones.

## Building from Source

```sh
# Build everything
dotnet build

# Run all tests
dotnet test

# Run tests with coverage (requires coverlet)
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Pack the CLI tool
dotnet pack src/MvvmMapper.Cli --output ./nupkg

# Install locally
dotnet tool install -g MvvmMapper --add-source ./nupkg
```

## Requirements

- .NET 8 SDK or later
- Windows, macOS, or Linux
- No internet connection required at runtime (all output is offline-safe)
