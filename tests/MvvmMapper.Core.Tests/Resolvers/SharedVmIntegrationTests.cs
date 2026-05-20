using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class SharedVmIntegrationTests
{
    private static readonly string s_samplesPath =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "samples", "shared-vm"));

    [Fact]
    public async Task SharedVm_ExplicitXamlResolver_EmitsThreeHighConfidenceEdgesToAuthViewModel()
    {
        // Use the real filesystem — shared-vm sample files exist on disk
        var fs = new SystemFileSystem();
        var config = new MvvmMapConfig();
        var discoverer = new FileDiscoverer(fs, NullLogger<FileDiscoverer>.Instance);
        var discovery = discoverer.Discover(s_samplesPath, config);

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new ExplicitXamlResolver(parser, NullLogger<ExplicitXamlResolver>.Instance);

        var result = await resolver.ResolveAsync(discovery);

        // Should have 3 BindsTo edges all pointing to AuthViewModel
        var bindsToEdges = result.Edges
            .Where(e => e.Kind == EdgeKind.BindsTo)
            .ToList();

        bindsToEdges.Should().HaveCount(3,
            "LoginView, RegisterView, and ForgotPasswordView all bind to AuthViewModel");

        bindsToEdges.Should().AllSatisfy(e =>
        {
            e.Confidence.Should().Be(Confidence.High);
            e.ToId.Should().Be("vm:SharedVmApp.ViewModels.AuthViewModel");
        });

        // All three Views should be distinct
        var fromIds = bindsToEdges.Select(e => e.FromId).ToHashSet();
        fromIds.Should().HaveCount(3, "each view produces a distinct edge");
        fromIds.Should().Contain("view:SharedVmApp.Views.LoginView");
        fromIds.Should().Contain("view:SharedVmApp.Views.RegisterView");
        fromIds.Should().Contain("view:SharedVmApp.Views.ForgotPasswordView");
    }
}
