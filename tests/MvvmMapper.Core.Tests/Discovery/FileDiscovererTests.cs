using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using Xunit;

namespace MvvmMapper.Core.Tests.Discovery;

public sealed class FileDiscovererTests
{
    [Fact]
    public void Discover_FindsXamlAndCsFiles()
    {
        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = "<UserControl/>",
            ["/root/ViewModels/LoginViewModel.cs"] = "class LoginViewModel {}",
            ["/root/bin/Debug/Temp.cs"] = "class Temp {}",
        });

        var discoverer = new FileDiscoverer(fs, NullLogger<FileDiscoverer>.Instance);
        var result = discoverer.Discover("/root", new MvvmMapConfig());

        result.XamlFiles.Should().ContainSingle(f => f.Contains("LoginView.xaml"));
        result.CsFiles.Should().ContainSingle(f => f.Contains("LoginViewModel.cs"));
        result.CsFiles.Should().NotContain(f => f.Contains("bin"));
    }
}
