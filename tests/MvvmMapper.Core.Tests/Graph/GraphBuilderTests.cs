using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Resolvers;
using Xunit;

namespace MvvmMapper.Core.Tests.Graph;

public sealed class GraphBuilderTests
{
    private sealed class StubResolver : IResolver
    {
        private readonly ResolverResult _result;
        public StubResolver(ResolverResult result) => _result = result;
        public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class ThrowingResolver : IResolver
    {
        public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated resolver failure");
    }

    [Fact]
    public async Task BuildAsync_NoResolvers_ReturnsEmptyGraph()
    {
        var builder = new GraphBuilder([], NullLogger<GraphBuilder>.Instance);
        var discovery = new DiscoveryResult([], [], "/root");

        var graph = await builder.BuildAsync(discovery);

        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_SingleResolver_AddsNodesAndEdges()
    {
        var viewNode = new ViewNode("view:v1", "LoginView", "lv.xaml", null, "");
        var vmNode = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel");
        var edge = new Edge("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test");

        var result = new ResolverResult([viewNode, vmNode], [edge]);
        var resolver = new StubResolver(result);

        var builder = new GraphBuilder([resolver], NullLogger<GraphBuilder>.Instance);
        var discovery = new DiscoveryResult([], [], "/root");

        var graph = await builder.BuildAsync(discovery);

        graph.Nodes.Should().ContainKey("view:v1");
        graph.Nodes.Should().ContainKey("vm:lvm");
        graph.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.BindsTo);
    }

    [Fact]
    public async Task BuildAsync_DuplicateEdges_MergesIntoOne()
    {
        var viewNode = new ViewNode("view:v1", "LoginView", "lv.xaml", null, "");
        var vmNode = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel");
        var edge1 = new Edge("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "reason1");
        var edge2 = new Edge("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.Low, "reason2");

        var result = new ResolverResult([viewNode, vmNode], [edge1, edge2]);
        var resolver = new StubResolver(result);

        var builder = new GraphBuilder([resolver], NullLogger<GraphBuilder>.Instance);
        var discovery = new DiscoveryResult([], [], "/root");

        var graph = await builder.BuildAsync(discovery);

        // Merged into a single edge
        graph.Edges.Should().ContainSingle(e =>
            e.FromId == "view:v1" && e.ToId == "vm:lvm" && e.Kind == EdgeKind.BindsTo);
    }

    [Fact]
    public async Task BuildAsync_FailingResolver_DoesNotThrow_OtherResolversStillRun()
    {
        var throwingResolver = new ThrowingResolver();

        var viewNode = new ViewNode("view:v1", "LoginView", "lv.xaml", null, "");
        var goodResult = new ResolverResult([viewNode], []);
        var goodResolver = new StubResolver(goodResult);

        var builder = new GraphBuilder([throwingResolver, goodResolver], NullLogger<GraphBuilder>.Instance);
        var discovery = new DiscoveryResult([], [], "/root");

        var act = async () => await builder.BuildAsync(discovery);
        await act.Should().NotThrowAsync();

        var graph = await builder.BuildAsync(discovery);
        graph.Nodes.Should().ContainKey("view:v1");
    }
}
