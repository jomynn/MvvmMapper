using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Resolvers.Endpoints;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class RestSharpResolverTests
{
    [Fact]
    public async Task Resolve_RestRequest_EmitsHighConfidenceEdge()
    {
        const string source = """
            namespace MyApp.Services;
            using System.Threading;
            using System.Threading.Tasks;
            public class OrderService
            {
                public async Task CreateOrderAsync(CancellationToken ct)
                {
                    var request = new RestRequest("/api/orders", Method.Post);
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Services/OrderService.cs"] = source
        });

        var resolver = new RestSharpResolver(fs, NullLogger<RestSharpResolver>.Instance);
        var discovery = new DiscoveryResult([], ["/root/Services/OrderService.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Hits && e.Confidence == Confidence.High);
        result.Edges[0].ToId.Should().Be("endpoint:POST:/api/orders");
    }
}
