---
name: mvvm-scanning
description: Use this skill when analyzing WPF MVVM C# code to resolve relationships between Views (.xaml), ViewModels (.cs), services, and HTTP endpoints. Covers all common WPF MVVM patterns: explicit DataContext, ViewModelLocator (MVVM Light / CommunityToolkit.Mvvm), code-behind assignment, DI container registration (Microsoft.Extensions.DependencyInjection, Autofac, Prism, Caliburn.Micro), DataTemplate-based implicit binding, navigation frameworks (Prism Regions, MVVM Light Messenger), and shared-VM scenarios where one ViewModel is bound by multiple Views. Also covers HTTP call detection in service classes: HttpClient, Refit, RestSharp, Flurl. Use this skill whenever writing resolver logic, deciding confidence levels for edges, or interpreting ambiguous XAML/C# constructs.
---

# Skill: MVVM Scanning

This skill encodes hard-won knowledge about how WPF MVVM applications are structured in the real world, so resolver logic in MvvmMapper produces correct, confidence-scored edges.

## Quick mental model

A WPF MVVM app has five layers we care about:

```
XAML (View)  →  DataContext  →  ViewModel  →  Service (DI'd)  →  HTTP call  →  Endpoint
```

Each arrow can be resolved by different patterns. Your job as a resolver author is to know **all** the patterns, score them by confidence, and emit edges with a clear `Reason` string explaining why the edge exists.

## Pattern catalog: View → ViewModel

### Pattern 1: Explicit XAML DataContext (High confidence)

```xml
<UserControl x:Class="MyApp.Views.LoginView"
             xmlns:vm="clr-namespace:MyApp.ViewModels">
  <UserControl.DataContext>
    <vm:LoginViewModel />
  </UserControl.DataContext>
  ...
</UserControl>
```

**How to detect:** XML element `<UserControl.DataContext>` (or `<Window.DataContext>`, `<Page.DataContext>`) containing a child element. Resolve the XML namespace prefix (`vm`) to the CLR namespace declared in `xmlns:vm="clr-namespace:..."`. The child element's local name is the VM class name.

**Edge:** `View --BindsTo[High]--> ViewModel`
**Reason:** `"Explicit <UserControl.DataContext> element in XAML"`

### Pattern 2: Code-behind assignment (High confidence)

```csharp
// LoginView.xaml.cs
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        this.DataContext = new LoginViewModel();
    }
}
```

Or via constructor injection:

```csharp
public LoginView(LoginViewModel vm)
{
    InitializeComponent();
    DataContext = vm;
}
```

**How to detect:** In the View's `.xaml.cs` partial class, find assignments to `this.DataContext` or `DataContext`. Use Roslyn's `SemanticModel.GetSymbolInfo` on the right-hand side to resolve the type. Constructor parameters typed as ViewModels also count.

**Edge:** `View --BindsTo[High]--> ViewModel`
**Reason:** `"DataContext assigned in code-behind constructor"`

### Pattern 3: ViewModelLocator (High confidence if Locator resolved)

The MVVM Light / CommunityToolkit.Mvvm pattern:

```xml
<UserControl DataContext="{Binding LoginVM, Source={StaticResource Locator}}">
```

```csharp
// ViewModelLocator.cs
public class ViewModelLocator
{
    public LoginViewModel LoginVM => Ioc.Default.GetRequiredService<LoginViewModel>();
    public RegisterViewModel RegisterVM => Ioc.Default.GetRequiredService<RegisterViewModel>();
}
```

```xml
<!-- App.xaml -->
<Application.Resources>
    <vm:ViewModelLocator x:Key="Locator" />
</Application.Resources>
```

**How to detect:**
1. Parse the `DataContext="{Binding ...}"` markup extension. Look for `Source={StaticResource X}` and `Path=Y` (or unnamed first positional arg = Path).
2. Find the resource `X` in `App.xaml` or merged dictionaries — its type is the Locator class.
3. In the Locator class, find a property named `Y`. Its return type is the resolved ViewModel.

**Edge:** `View --BindsTo[High]--> ViewModel`
**Reason:** `"Locator binding: {Binding Y, Source={StaticResource X}} resolved via ViewModelLocator.Y"`

If the Locator class cannot be found or the property doesn't exist: downgrade to **Low** with reason `"Locator pattern detected but target property could not be resolved"`.

### Pattern 4: DI container registration (Medium confidence)

```csharp
// App.xaml.cs
services.AddSingleton<LoginView>();
services.AddTransient<LoginViewModel>();

// LoginView constructor takes LoginViewModel — DI injects it
```

**How to detect:** Find DI registration calls (`AddSingleton`, `AddTransient`, `AddScoped`, or container-specific equivalents like `builder.RegisterType<>().As<>()` for Autofac). When both a View and a VM are registered, AND the View's constructor takes that VM as a parameter, emit a Medium edge.

**Edge:** `View --BindsTo[Medium]--> ViewModel`
**Reason:** `"View and ViewModel both registered in DI; View constructor accepts ViewModel"`

### Pattern 5: Naming convention fallback (Low confidence)

`LoginView.xaml` paired with `LoginViewModel.cs`. Configured suffixes drive the match (defaults: `View`/`Page`/`Window` ↔ `ViewModel`/`VM`).

**Edge:** `View --BindsTo[Low]--> ViewModel`
**Reason:** `"Naming convention match: LoginView ↔ LoginViewModel"`

Emit this **even when** a higher-confidence resolver already matched, so the user can see all the signals. The renderer filters by `--confidence`.

### Pattern 6: DataTemplate implicit binding (High confidence, but different edge kind)

```xml
<DataTemplate DataType="{x:Type vm:ItemViewModel}">
    <views:ItemView />
</DataTemplate>
```

This is WPF saying "whenever the visual tree encounters an `ItemViewModel` instance, render it using `ItemView`." It's a View ↔ VM relationship driven by the VM, not the View.

**Edge:** `View --BindsTo[High]--> ViewModel`
**Reason:** `"DataTemplate with DataType={x:Type vm:ItemViewModel}"`

### Pattern 7: Caliburn.Micro convention (Low confidence unless detected)

Caliburn.Micro does naming-convention binding automatically — no DataContext written anywhere. If the project references `Caliburn.Micro`, naming convention should be promoted from Low to **Medium**.

## Pattern catalog: ViewModel → Method (via Command)

### ICommand property pattern

```csharp
public ICommand LoginCommand { get; }

public LoginViewModel()
{
    LoginCommand = new RelayCommand(ExecuteLogin);
}

private async void ExecuteLogin() { ... }
```

**How to detect:** Walk the constructor body. For each `new RelayCommand(...)`, `new DelegateCommand(...)`, `new AsyncRelayCommand(...)`, etc., the first argument is the Execute delegate. Resolve it to a method symbol.

XAML side:
```xml
<Button Command="{Binding LoginCommand}" />
```

**Edge chain:**
- `View --Invokes[High]--> Method` (via `Command` binding + property resolution)
- `ViewModel --Contains[High]--> Method`

### CommunityToolkit.Mvvm `[RelayCommand]` source generator

```csharp
[RelayCommand]
private async Task LoginAsync() { ... }
```

The source generator creates a `LoginCommand` property at compile time. Roslyn sees the generated symbol if you load via `MSBuildWorkspace` with build complete. Match the generated property back to the source method by stripping the `Async` suffix and adding `Command`.

**Edge:** `ViewModel --Contains[High]--> Method`, Reason: `"[RelayCommand] generator: LoginAsync → LoginCommand"`

## Pattern catalog: Method → Endpoint

### Raw HttpClient (variable confidence)

```csharp
var response = await _httpClient.PostAsync(
    "https://api.example.com/auth/login",
    content);
```

**High** if the URL is a string literal. **Medium** if it's an interpolated string with known prefix. **Low** if fully dynamic (variable concatenation).

Verbs to detect: `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `PatchAsync`, `SendAsync` (extract verb from `HttpRequestMessage` constructor).

### Refit interfaces (High confidence)

```csharp
public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync(LoginRequest req);
}
```

**How to detect:** Interfaces where methods carry `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Patch]`, `[Head]`, `[Options]` attributes from `Refit` namespace. Extract route from the attribute's first constructor argument.

**Edge:** `Method --Hits[High]--> Endpoint(POST, /api/auth/login)`
**Reason:** `"Refit [Post(\"/api/auth/login\")] attribute on IAuthApi.LoginAsync"`

When a VM injects `IAuthApi` and calls `_authApi.LoginAsync(...)`, emit:
- `ViewModelMethod --Calls[High]--> InterfaceMethod`
- `InterfaceMethod --Hits[High]--> Endpoint`

### RestSharp (High confidence)

```csharp
var request = new RestRequest("/api/auth/login", Method.Post);
await _client.ExecuteAsync(request);
```

Detect `new RestRequest(...)` instantiation, extract route from first arg, verb from second arg (`Method.Get`, `Method.Post`, etc.).

### Flurl (Medium confidence, often dynamic)

```csharp
await "https://api.example.com"
    .AppendPathSegment("auth/login")
    .PostJsonAsync(payload);
```

Detect the fluent chain. Concatenate string literal segments. Mark **Medium** because Flurl URLs are commonly composed dynamically.

## Resolving DI: tracing services through the graph

When `LoginViewModel` has constructor `LoginViewModel(IAuthService authService)`, you need to know what `IAuthService` actually does. Steps:

1. Find the interface `IAuthService` symbol via Roslyn.
2. Find all classes implementing it: `INamedTypeSymbol.AllInterfaces` check across the compilation.
3. Verify DI registration: scan `App.xaml.cs` / `Startup.cs` / `Program.cs` for `services.AddX<IAuthService, AuthService>()`. If found, that's the concrete impl.
4. If multiple impls exist and no DI registration disambiguates: emit edges to all impls with **Low** confidence and reason `"Multiple implementations of IAuthService; could not disambiguate"`.

## Shared ViewModel detection

After all resolvers run, compute fan-in per ViewModelNode:

```
fanIn(vm) = count of edges where edge.ToId == vm.Id and edge.Kind == BindsTo
```

If `fanIn(vm) >= 2`, mark the VM as shared. The HTML report should:
- Show a 🔗 icon next to the VM name
- List all binding Views
- In the "By Endpoint" pivot, when an endpoint is reached via a shared VM, show all upstream Views

## Edge cases the resolvers must handle

| Case | Handling |
|---|---|
| XAML uses `mc:Ignorable` design-time DataContext | Ignore `d:DataContext` — design-time only |
| Generic VMs (`ListViewModel<Product>`) | Treat as distinct nodes per closed generic; use `OriginalDefinition` for grouping |
| VM in a different assembly | Cross-project resolution via `Compilation.GlobalNamespace`; respect project references |
| XAML namespace alias collision | Resolve `xmlns:vm` per-file, not globally |
| Conditional compilation (`#if DEBUG`) | Roslyn sees only the active build configuration; document this limitation in README |
| Source-generated VMs (`[INotifyPropertyChanged]`) | Generators run as part of `MSBuildWorkspace` compilation — symbols are present |
| Partial classes split across files | Aggregate members via `INamedTypeSymbol.GetMembers()` not per-syntax-tree |
| Async void Execute methods | Same as async Task — resolve normally |

## Pitfalls to avoid

- **Don't** resolve XAML `{Binding}` paths as ViewModel-to-VM edges. They're property accesses within a VM, not VM-to-VM relationships. The exception is `DataContext="{Binding SomeChildVM}"` which IS a composition edge.
- **Don't** treat every type injected into a VM as a Service. Logger types, `IOptions<T>`, `IMapper` (AutoMapper), event aggregators — these are infrastructure. Maintain a configurable allow/deny list.
- **Don't** trust file paths to match namespaces. Use Roslyn's `SemanticModel` to get the actual namespace.
- **Don't** assume one View = one VM. Composed Views (nested UserControls) introduce multiple VMs into one visual tree.

## Confidence scoring rubric

| Signal | Confidence |
|---|---|
| Symbol resolved unambiguously by Roslyn semantic model | High |
| Explicit XAML element with resolvable namespace | High |
| String literal route in HTTP attribute or call | High |
| DI registration + matching constructor parameter | Medium |
| Interpolated string with known prefix | Medium |
| Caliburn.Micro project + naming convention match | Medium |
| Pure naming convention with no other signal | Low |
| Locator detected but property unresolved | Low |
| Dynamic URL composition | Low |

When multiple resolvers find the same edge, keep the **highest** confidence and concatenate reasons: `"Explicit XAML; also matches naming convention"`.

## Quick reference: which Roslyn APIs to use

| Task | API |
|---|---|
| Open `.sln` | `MSBuildWorkspace.Create().OpenSolutionAsync()` (call `MSBuildLocator.RegisterDefaults()` first) |
| Get compilation | `Project.GetCompilationAsync()` |
| Get semantic model | `Compilation.GetSemanticModel(syntaxTree)` — cache per tree |
| Resolve type at syntax node | `SemanticModel.GetSymbolInfo(node).Symbol as INamedTypeSymbol` |
| Find all classes implementing interface | iterate `INamedTypeSymbol`s, check `AllInterfaces.Contains(iface)` |
| Get attributes on member | `ISymbol.GetAttributes()` |
| Walk class members across partials | `INamedTypeSymbol.GetMembers()` |
| Detect attribute by name (avoid string compare) | `attr.AttributeClass?.ToDisplayString() == "Refit.PostAttribute"` |

Keep these in mind; reaching for syntax-only APIs (`SyntaxNode.ChildNodes()`) without the semantic model is the #1 source of false positives.
