using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Resolvers.Endpoints;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class HttpClientResolverTests
{
    [Fact]
    public async Task Resolve_StringLiteralUrl_EmitsHighConfidenceEdge()
    {
        const string source = """
            namespace MyApp.Services;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            public class AuthService
            {
                private readonly HttpClient _httpClient;
                public AuthService(HttpClient httpClient) { _httpClient = httpClient; }
                public async Task LoginAsync(CancellationToken ct)
                {
                    await _httpClient.PostAsync("/api/auth/login", null, ct);
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Services/AuthService.cs"] = source
        });

        var resolver = new HttpClientResolver(fs, NullLogger<HttpClientResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Services/AuthService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Hits && e.Confidence == Confidence.High);
        var edge = result.Edges[0];
        edge.ToId.Should().Be("endpoint:POST:/api/auth/login");
    }

    [Fact]
    public async Task Resolve_InterpolatedUrl_EmitsMediumConfidenceEdge()
    {
        const string source = """
            namespace MyApp.Services;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            public class UserService
            {
                private readonly HttpClient _httpClient;
                private readonly string _baseUrl = "https://api.example.com";
                public UserService(HttpClient httpClient) { _httpClient = httpClient; }
                public async Task GetUserAsync(int id, CancellationToken ct)
                {
                    await _httpClient.GetAsync($"{_baseUrl}/api/users/{id}", ct);
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Services/UserService.cs"] = source
        });

        var resolver = new HttpClientResolver(fs, NullLogger<HttpClientResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Services/UserService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Hits && e.Confidence == Confidence.Medium);
    }

    [Fact]
    public async Task Resolve_SendAsync_ExtractsVerbAndRoute()
    {
        const string source = """
            namespace MyApp.Services;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            public class AuthService
            {
                private readonly HttpClient _httpClient;
                public AuthService(HttpClient h) { _httpClient = h; }
                public async Task DeleteAsync(CancellationToken ct)
                {
                    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/1"), ct);
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Services/AuthService.cs"] = source
        });

        var resolver = new HttpClientResolver(fs, NullLogger<HttpClientResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Services/AuthService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Hits);
        result.Edges[0].ToId.Should().Be("endpoint:DELETE:/api/users/1");
    }
}
