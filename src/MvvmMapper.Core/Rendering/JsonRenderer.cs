using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Rendering;

public sealed class JsonRenderer(ILogger<JsonRenderer> logger) : IRenderer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Format => "json";

    public async Task RenderAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var sharedVmCount = graph.Nodes.Values
            .Where(n => n.Kind == NodeKind.ViewModel)
            .Count(vm => graph.EdgesTo(vm.Id).Count(e => e.Kind == EdgeKind.BindsTo) >= 2);

        var payload = new
        {
            generatedAt = DateTime.UtcNow,
            summary = new
            {
                nodeCount = graph.Nodes.Count,
                edgeCount = graph.Edges.Count,
                sharedViewModels = sharedVmCount
            },
            nodes = graph.Nodes.Values.Select(n => new
            {
                n.Id,
                n.DisplayName,
                kind = n.Kind.ToString(),
                n.SourceFile,
                n.SourceLine
            }),
            edges = graph.Edges.Select(e => new
            {
                e.FromId,
                e.ToId,
                kind = e.Kind.ToString(),
                confidence = e.Confidence.ToString(),
                e.Reason
            })
        };

        var outputFile = Path.Combine(outputDirectory, "graph.json");
        var json = JsonSerializer.Serialize(payload, s_options);
        await File.WriteAllTextAsync(outputFile, json, cancellationToken);
        logger.LogInformation("Wrote {File}", outputFile);
    }
}
