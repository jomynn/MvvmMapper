using System.CommandLine;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Rendering;
using MvvmMapper.Core.Resolvers;
using MvvmMapper.Core.Resolvers.Commands;
using MvvmMapper.Core.Resolvers.Endpoints;
using MvvmMapper.Core.Resolvers.ViewToViewModel;

namespace MvvmMapper.Cli.Commands;

internal static class ScanCommand
{
    public static Command Build(ILoggerFactory loggerFactory)
    {
        var pathArg = new Argument<string>("path", "Path to .sln, .csproj, or folder to scan");
        var outputOption = new Option<string>("--output", () => "./mvvm-map-output", "Output directory");
        var formatOption = new Option<string>("--format", () => "all", "Output format: html|mermaid|json|all");
        var confidenceOption = new Option<string>("--confidence", () => "low", "Minimum confidence to include: high|medium|low");
        var configOption = new Option<string?>("--config", () => null, "Path to mvvm-map.json");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

        var cmd = new Command("scan", "Scan a WPF MVVM solution and generate a relationship map")
        {
            pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption
        };

        cmd.SetHandler(async (path, output, format, confidence, config, verbose) =>
        {
            _ = confidence;
            _ = verbose;

            var fs = new SystemFileSystem();
            var configLoader = new ConfigLoader(fs, loggerFactory.CreateLogger<ConfigLoader>());
            var mvvmConfig = configLoader.Load(config);

            var solutionLoader = new SolutionLoader(loggerFactory.CreateLogger<SolutionLoader>());
            var root = solutionLoader.ResolveRootDirectory(path);

            var discoverer = new FileDiscoverer(fs, loggerFactory.CreateLogger<FileDiscoverer>());
            var discovery = discoverer.Discover(root, mvvmConfig);

            var xamlParser = new XamlParser(fs, loggerFactory.CreateLogger<XamlParser>());

            var resolvers = new IResolver[]
            {
                new ExplicitXamlResolver(xamlParser, loggerFactory.CreateLogger<ExplicitXamlResolver>()),
                new CodeBehindResolver(fs, xamlParser, loggerFactory.CreateLogger<CodeBehindResolver>()),
                new LocatorResolver(mvvmConfig, xamlParser, fs, loggerFactory.CreateLogger<LocatorResolver>()),
                new DiContainerResolver(mvvmConfig, xamlParser, fs, loggerFactory.CreateLogger<DiContainerResolver>()),
                new NamingConventionResolver(mvvmConfig, xamlParser, fs, loggerFactory.CreateLogger<NamingConventionResolver>()),
                new CommandResolver(mvvmConfig, xamlParser, fs, loggerFactory.CreateLogger<CommandResolver>()),
                new HttpClientResolver(fs, loggerFactory.CreateLogger<HttpClientResolver>()),
                new RefitResolver(fs, loggerFactory.CreateLogger<RefitResolver>()),
                new RestSharpResolver(fs, loggerFactory.CreateLogger<RestSharpResolver>()),
            };

            var graphBuilder = new GraphBuilder(resolvers, loggerFactory.CreateLogger<GraphBuilder>());
            var graph = await graphBuilder.BuildAsync(discovery);

            Directory.CreateDirectory(output);

            var renderers = BuildRenderers(loggerFactory, format);

            foreach (var renderer in renderers)
            {
                await renderer.RenderAsync(graph, output);
            }

            Console.WriteLine($"Scan complete. Output written to: {Path.GetFullPath(output)}");
            Console.WriteLine($"  Nodes : {graph.Nodes.Count}");
            Console.WriteLine($"  Edges : {graph.Edges.Count}");

            if (renderers.Any(r => r.Format == "html"))
                Console.WriteLine($"  Report: {Path.Combine(Path.GetFullPath(output), "report.html")}");
            if (renderers.Any(r => r.Format == "json"))
                Console.WriteLine($"  JSON  : {Path.Combine(Path.GetFullPath(output), "graph.json")}");
            if (renderers.Any(r => r.Format == "mermaid"))
                Console.WriteLine($"  MD    : {Path.Combine(Path.GetFullPath(output), "mermaid-by-view.md")}");

        }, pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption);

        return cmd;
    }

    private static IRenderer[] BuildRenderers(ILoggerFactory loggerFactory, string format)
    {
        var all = new IRenderer[]
        {
            new JsonRenderer(loggerFactory.CreateLogger<JsonRenderer>()),
            new MermaidRenderer(loggerFactory.CreateLogger<MermaidRenderer>()),
            new HtmlRenderer(loggerFactory.CreateLogger<HtmlRenderer>()),
        };

        if (format.Equals("all", StringComparison.OrdinalIgnoreCase))
            return all;

        var formats = format.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return all.Where(r => formats.Any(f => f.Equals(r.Format, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
