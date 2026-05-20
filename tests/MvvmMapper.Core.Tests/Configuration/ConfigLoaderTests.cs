using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using Xunit;

namespace MvvmMapper.Core.Tests.Configuration;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void Load_FileNotFound_ReturnsDefaults()
    {
        var fs = new FakeFileSystem(new Dictionary<string, string>());
        var loader = new ConfigLoader(fs, NullLogger<ConfigLoader>.Instance);

        var config = loader.Load(null);

        config.Should().NotBeNull();
        config.Patterns.ViewModelSuffix.Should().Contain("ViewModel");
        config.LocatorClasses.Should().Contain("ViewModelLocator");
    }

    [Fact]
    public void Load_ValidJsonFile_DeserializesConfig()
    {
        const string json = """
            {
              "locatorClasses": ["MyLocator"],
              "patterns": {
                "viewModelSuffix": ["VM", "ViewModel"]
              }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/config/mvvm-map.json"] = json
        });
        var loader = new ConfigLoader(fs, NullLogger<ConfigLoader>.Instance);

        var config = loader.Load("/config/mvvm-map.json");

        config.LocatorClasses.Should().Contain("MyLocator");
        config.Patterns.ViewModelSuffix.Should().Contain("VM");
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        const string badJson = "{ not valid json {{{{";

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/config/mvvm-map.json"] = badJson
        });
        var loader = new ConfigLoader(fs, NullLogger<ConfigLoader>.Instance);

        // Should not throw — should log warning and return defaults
        var config = loader.Load("/config/mvvm-map.json");

        config.Should().NotBeNull();
        config.Patterns.ViewModelSuffix.Should().Contain("ViewModel");
    }

    [Fact]
    public void Load_NullConfigPath_UsesDefaultFileName_AndReturnsDefaultsWhenMissing()
    {
        // FakeFileSystem has no file named "mvvm-map.json"
        var fs = new FakeFileSystem(new Dictionary<string, string>());
        var loader = new ConfigLoader(fs, NullLogger<ConfigLoader>.Instance);

        var config = loader.Load(null);

        config.Should().NotBeNull();
    }
}
