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
}
