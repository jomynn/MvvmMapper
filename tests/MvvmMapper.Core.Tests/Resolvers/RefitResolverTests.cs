using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Resolvers.Endpoints;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class RefitResolverTests
{
    [Fact]
    public async Task Resolve_RefitInterface_EmitsHighConfidenceEdge()
    {
        const string source = """
            namespace MyApp.Api;
            using System.Threading.Tasks;
            public interface IAuthApi
            {
                [Post("/api/auth/login")]
                Task<string> LoginAsync(string req);

                [Get("/api/users")]
                Task<string[]> GetUsersAsync();
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Api/IAuthApi.cs"] = source
        });

        var resolver = new RefitResolver(fs, NullLogger<RefitResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Api/IAuthApi.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().HaveCount(2);
        result.Edges.Should().AllSatisfy(e =>
        {
            e.Kind.Should().Be(EdgeKind.Hits);
            e.Confidence.Should().Be(Confidence.High);
        });
        result.Edges.Select(e => e.ToId).Should()
            .Contain("endpoint:POST:/api/auth/login")
            .And.Contain("endpoint:GET:/api/users");
    }

    [Fact]
    public async Task Resolve_CallerUsesRefitInterface_EmitsCallsEdge()
    {
        const string interfaceSource = """
            namespace MyApp.Api;
            using System.Threading.Tasks;
            public interface IAuthApi
            {
                [Post("/api/auth/login")]
                Task<string> LoginAsync(string req);
            }
            """;

        const string callerSource = """
            namespace MyApp.Services;
            using MyApp.Api;
            using System.Threading.Tasks;
            public class AuthService
            {
                private readonly IAuthApi _authApi;
                public AuthService(IAuthApi authApi) { _authApi = authApi; }
                public async Task<string> LoginAsync(string req) => await _authApi.LoginAsync(req);
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Api/IAuthApi.cs"] = interfaceSource,
            ["/root/Services/AuthService.cs"] = callerSource
        });

        var resolver = new RefitResolver(fs, NullLogger<RefitResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Api/IAuthApi.cs", "/root/Services/AuthService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        // Should have: Hits edge (interface method→endpoint) + Calls edge (caller→interface method)
        result.Edges.Should().Contain(e => e.Kind == EdgeKind.Hits);
        result.Edges.Should().Contain(e => e.Kind == EdgeKind.Calls);
    }

    [Fact]
    public async Task Resolve_NoRefitInterface_EmitsNoEdge()
    {
        const string source = """
            namespace MyApp.Services;
            public class AuthService
            {
                public void Login() { }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Services/AuthService.cs"] = source
        });

        var resolver = new RefitResolver(fs, NullLogger<RefitResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Services/AuthService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().BeEmpty();
    }
}
