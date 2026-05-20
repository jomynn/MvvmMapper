namespace MvvmMapper.Core.Configuration;

public sealed record MvvmMapConfig
{
    public PatternConfig Patterns { get; init; } = new();
    public string[] DiContainers { get; init; } = ["Microsoft.Extensions.DependencyInjection", "Autofac", "SimpleInjector"];
    public string[] LocatorClasses { get; init; } = ["ViewModelLocator"];
    public string[] HttpClientTypes { get; init; } = ["HttpClient", "IRestClient", "IFlurlClient"];
    public Dictionary<string, string> EndpointBaseUrls { get; init; } = new() { ["default"] = "https://api.example.com" };
    public string[] Exclude { get; init; } = ["**/Generated/**", "**/obj/**", "**/bin/**"];
}

public sealed record PatternConfig
{
    public string[] ViewSuffix { get; init; } = ["View", "Page", "Window"];
    public string[] ViewModelSuffix { get; init; } = ["ViewModel", "VM"];
    public string[] ViewModelBaseTypes { get; init; } = ["ObservableObject", "ViewModelBase", "BindableBase", "ReactiveObject"];
}
