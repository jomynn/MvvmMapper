using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Resolvers.Endpoints;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class EndpointClassResolverTests
{
    private static EndpointClassResolver CreateResolver(Dictionary<string, string> files) =>
        new(new FakeFileSystem(files), NullLogger<EndpointClassResolver>.Instance);

    private static DiscoveryResult Discovery(params string[] paths) =>
        new([], [.. paths], "/root");

    // ────────────────────────────────────────────────────────────────────────
    // Endpoint class extraction
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_EndpointClassWithUrls_EmitsEndpointNodes()
    {
        const string source = """
            namespace MyApp.Endpoints;
            public class ForeignDraftEndpoint
            {
                public static System.Collections.Generic.Dictionary<RequestCode, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<RequestCode, string>
                    {
                        [RequestCode.IssueForeignDraft] = "/api/foreign-draft/issue",
                        [RequestCode.GetForeignDraft]   = "/api/foreign-draft/{id}",
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/ForeignDraftEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/ForeignDraftEndpoint.cs"));

        result.Nodes.OfType<EndpointNode>().Should().HaveCount(2);
        result.Nodes.OfType<EndpointNode>().Should().Contain(n => n.Verb == "POST" && n.Route == "/api/foreign-draft/issue");
        result.Nodes.OfType<EndpointNode>().Should().Contain(n => n.Verb == "GET" && n.Route == "/api/foreign-draft/{id}");
    }

    [Fact]
    public async Task Resolve_EndpointClassWithRoutes_EmitsEndpointNodes()
    {
        const string source = """
            namespace MyApp.Api;
            public class UserEndpoint
            {
                public static System.Collections.Generic.Dictionary<UserAction, string> Routes { get; }
                    = new System.Collections.Generic.Dictionary<UserAction, string>
                    {
                        [UserAction.FetchAll] = "/api/users",
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/UserEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/UserEndpoint.cs"));

        result.Nodes.OfType<EndpointNode>().Should().ContainSingle();
        result.Nodes.OfType<EndpointNode>().First().Verb.Should().Be("GET");
    }

    [Fact]
    public async Task Resolve_ClassNotEndingSuffixEndpoint_Ignored()
    {
        const string source = """
            namespace MyApp;
            public class AuthRouteConfig
            {
                public static System.Collections.Generic.Dictionary<string, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["login"] = "/api/auth/login",
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/AuthRouteConfig.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/AuthRouteConfig.cs"));

        result.Nodes.OfType<EndpointNode>().Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_NonStaticUrlProperty_Ignored()
    {
        const string source = """
            namespace MyApp;
            public class AuthEndpoint
            {
                public System.Collections.Generic.Dictionary<string, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["login"] = "/api/auth/login",
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/AuthEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/AuthEndpoint.cs"));

        result.Nodes.OfType<EndpointNode>().Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_DictionaryValueNotString_Ignored()
    {
        const string source = """
            namespace MyApp;
            public class AuthEndpoint
            {
                public static System.Collections.Generic.Dictionary<string, int> Urls { get; }
                    = new System.Collections.Generic.Dictionary<string, int>
                    {
                        ["login"] = 42,
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/AuthEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/AuthEndpoint.cs"));

        result.Nodes.OfType<EndpointNode>().Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Verb inference
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GetUser",          "GET")]
    [InlineData("FetchAll",         "GET")]
    [InlineData("ListOrders",       "GET")]
    [InlineData("LoadProfile",      "GET")]
    [InlineData("SearchItems",      "GET")]
    [InlineData("FindById",         "GET")]
    [InlineData("DeleteRecord",     "DELETE")]
    [InlineData("RemoveItem",       "DELETE")]
    [InlineData("CancelOrder",      "DELETE")]
    [InlineData("PatchStatus",      "PATCH")]
    [InlineData("PutResource",      "PUT")]
    [InlineData("ReplaceUser",      "PUT")]
    [InlineData("IssueDraft",       "POST")]
    [InlineData("SubmitForm",       "POST")]
    [InlineData("CreateOrder",      "POST")]
    [InlineData("SendNotification", "POST")]
    [InlineData("UpdateName",       "POST")]
    public async Task Resolve_VerbInferredFromEnumKeyPrefix(string enumMember, string expectedVerb)
    {
        var source = $$"""
            namespace MyApp;
            public class DemoEndpoint
            {
                public static System.Collections.Generic.Dictionary<ActionCode, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<ActionCode, string>
                    {
                        [ActionCode.{{enumMember}}] = "/api/demo",
                    };
            }
            """;

        var resolver = CreateResolver(new() { ["/root/DemoEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/DemoEndpoint.cs"));

        var endpoint = result.Nodes.OfType<EndpointNode>().Should().ContainSingle().Subject;
        endpoint.Verb.Should().Be(expectedVerb);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Caller linkage (HttpClient using ClassName.Urls[Key])
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_CallerUsesEndpointClassUrl_EmitsMediumHitsEdge()
    {
        const string endpointSource = """
            namespace MyApp.Endpoints;
            public class ForeignDraftEndpoint
            {
                public static System.Collections.Generic.Dictionary<RequestCode, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<RequestCode, string>
                    {
                        [RequestCode.IssueForeignDraft] = "/api/foreign-draft/issue",
                    };
            }
            """;

        const string serviceSource = """
            namespace MyApp.Services;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            using MyApp.Endpoints;
            public class DraftService
            {
                private readonly HttpClient _http;
                public DraftService(HttpClient http) { _http = http; }
                public async Task IssueAsync(CancellationToken ct)
                {
                    await _http.PostAsync(ForeignDraftEndpoint.Urls[RequestCode.IssueForeignDraft], null, ct);
                }
            }
            """;

        var files = new Dictionary<string, string>
        {
            ["/root/Endpoints/ForeignDraftEndpoint.cs"] = endpointSource,
            ["/root/Services/DraftService.cs"] = serviceSource,
        };

        var resolver = CreateResolver(files);
        var result = await resolver.ResolveAsync(Discovery(
            "/root/Endpoints/ForeignDraftEndpoint.cs",
            "/root/Services/DraftService.cs"));

        var hitsEdges = result.Edges.Where(e => e.Kind == EdgeKind.Hits).ToList();
        hitsEdges.Should().ContainSingle();
        hitsEdges[0].Confidence.Should().Be(Confidence.Medium);
        hitsEdges[0].ToId.Should().Be("endpoint:POST:/api/foreign-draft/issue");
    }

    [Fact]
    public async Task Resolve_CallerUnresolvableKey_EmitsLowConfidenceEdgesForAllEntries()
    {
        const string endpointSource = """
            namespace MyApp.Endpoints;
            public class OrderEndpoint
            {
                public static System.Collections.Generic.Dictionary<OrderCode, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<OrderCode, string>
                    {
                        [OrderCode.GetOrder]    = "/api/orders/{id}",
                        [OrderCode.CreateOrder] = "/api/orders",
                    };
            }
            """;

        const string serviceSource = """
            namespace MyApp.Services;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;
            using MyApp.Endpoints;
            public class OrderService
            {
                private readonly HttpClient _http;
                public OrderService(HttpClient http) { _http = http; }
                public async Task DoAsync(OrderCode code, System.Net.Http.HttpContent body, CancellationToken ct)
                {
                    await _http.PostAsync(OrderEndpoint.Urls[code], body, ct);
                }
            }
            """;

        var files = new Dictionary<string, string>
        {
            ["/root/Endpoints/OrderEndpoint.cs"] = endpointSource,
            ["/root/Services/OrderService.cs"] = serviceSource,
        };

        var resolver = CreateResolver(files);
        var result = await resolver.ResolveAsync(Discovery(
            "/root/Endpoints/OrderEndpoint.cs",
            "/root/Services/OrderService.cs"));

        var hitsEdges = result.Edges.Where(e => e.Kind == EdgeKind.Hits).ToList();
        hitsEdges.Should().HaveCount(2);
        hitsEdges.Should().AllSatisfy(e => e.Confidence.Should().Be(Confidence.Low));
    }

    [Fact]
    public async Task Resolve_EndpointClassWithNoStringValues_EmitsNoNodes()
    {
        const string source = """
            namespace MyApp;
            public class EmptyEndpoint
            {
                public static System.Collections.Generic.Dictionary<RequestCode, string> Urls { get; }
                    = new System.Collections.Generic.Dictionary<RequestCode, string>();
            }
            """;

        var resolver = CreateResolver(new() { ["/root/EmptyEndpoint.cs"] = source });
        var result = await resolver.ResolveAsync(Discovery("/root/EmptyEndpoint.cs"));

        result.Nodes.OfType<EndpointNode>().Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }
}
