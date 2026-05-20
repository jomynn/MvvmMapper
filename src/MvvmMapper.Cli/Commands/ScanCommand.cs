using System.CommandLine;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core;
using MvvmMapper.Core.Analysis;
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
        var watchOption = new Option<bool>("--watch", "Re-scan on file changes (Ctrl+C to stop)");

        var cmd = new Command("scan", "Scan a WPF MVVM solution and generate a relationship map")
        {
            pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption, watchOption
        };

        cmd.SetHandler(async (path, output, format, confidence, config, verbose, watch) =>
        {
            _ = confidence;
            _ = verbose;

            var fs = new SystemFileSystem();
            var configLoader = new ConfigLoader(fs, loggerFactory.CreateLogger<ConfigLoader>());
            var mvvmConfig = configLoader.Load(config);

            var solutionLoader = new SolutionLoader(loggerFactory.CreateLogger<SolutionLoader>());
            var root = solutionLoader.ResolveRootDirectory(path);

            var analysisRunner = new AnalysisRunner(
                new OrphanDetector(),
                new FanOutAnalyzer(),
                new EndpointImpactAnalyzer());

            async Task RunScanAsync(CancellationToken ct)
            {
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
                    new DataTemplateResolver(xamlParser, loggerFactory.CreateLogger<DataTemplateResolver>()),
                    new CommandResolver(mvvmConfig, xamlParser, fs, loggerFactory.CreateLogger<CommandResolver>()),
                    new HttpClientResolver(fs, loggerFactory.CreateLogger<HttpClientResolver>()),
                    new RefitResolver(fs, loggerFactory.CreateLogger<RefitResolver>()),
                    new RestSharpResolver(fs, loggerFactory.CreateLogger<RestSharpResolver>()),
                    new EndpointClassResolver(fs, loggerFactory.CreateLogger<EndpointClassResolver>()),
                };

                var graphBuilder = new GraphBuilder(resolvers, loggerFactory.CreateLogger<GraphBuilder>());
                var graph = await graphBuilder.BuildAsync(discovery, ct);

                var analysisReport = analysisRunner.Run(graph);

                Directory.CreateDirectory(output);

                var renderers = BuildRenderers(loggerFactory, format);

                foreach (var renderer in renderers)
                {
                    if (renderer is HtmlRenderer htmlRenderer)
                        htmlRenderer.AnalysisReport = analysisReport;

                    await renderer.RenderAsync(graph, output, ct);
                }

                Console.WriteLine($"Scan complete. Output written to: {Path.GetFullPath(output)}");
                Console.WriteLine($"  Nodes : {graph.Nodes.Count}");
                Console.WriteLine($"  Edges : {graph.Edges.Count}");

                // Analysis summary
                if (analysisReport.OrphanedViews.Count > 0)
                    Console.WriteLine($"  Orphan Views    : {analysisReport.OrphanedViews.Count} ({string.Join(", ", analysisReport.OrphanedViews.Select(o => o.DisplayName))})");
                if (analysisReport.OrphanedViewModels.Count > 0)
                    Console.WriteLine($"  Orphan VMs      : {analysisReport.OrphanedViewModels.Count} ({string.Join(", ", analysisReport.OrphanedViewModels.Select(o => o.DisplayName))})");
                if (analysisReport.UnreachableEndpoints.Count > 0)
                    Console.WriteLine($"  Unreachable Eps : {analysisReport.UnreachableEndpoints.Count} ({string.Join(", ", analysisReport.UnreachableEndpoints.Select(o => o.DisplayName))})");
                if (analysisReport.SharedViewModels.Count > 0)
                {
                    Console.WriteLine($"  Shared VMs      : {analysisReport.SharedViewModels.Count}");
                    foreach (var svm in analysisReport.SharedViewModels)
                        Console.WriteLine($"    {svm.DisplayName} (fan-in={svm.FanIn})");
                }

                if (renderers.Any(r => r.Format == "html"))
                    Console.WriteLine($"  Report: {Path.Combine(Path.GetFullPath(output), "report.html")}");
                if (renderers.Any(r => r.Format == "json"))
                    Console.WriteLine($"  JSON  : {Path.Combine(Path.GetFullPath(output), "graph.json")}");
                if (renderers.Any(r => r.Format == "mermaid"))
                    Console.WriteLine($"  MD    : {Path.Combine(Path.GetFullPath(output), "mermaid-by-view.md")}");
            }

            await RunScanAsync(CancellationToken.None);

            if (watch)
            {
                Console.WriteLine($"Watching {root} for changes... (Ctrl+C to stop)");
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                System.Threading.Timer? debounceTimer = null;

                void OnChanged(object sender, FileSystemEventArgs e)
                {
                    var name = e.Name;
                    if (name is null) return;
                    if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) return;

                    var old = System.Threading.Interlocked.Exchange(ref debounceTimer, null);
                    old?.Dispose();

                    debounceTimer = new System.Threading.Timer(_ =>
                    {
                        Console.WriteLine($"Change detected: {e.Name}. Rescanning...");
                        Task.Run(() =>
                        {
                            try
                            {
                                RunScanAsync(cts.Token).GetAwaiter().GetResult();
                            }
                            catch (OperationCanceledException) { }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error during rescan: {ex.Message}");
                            }
                        });
                    }, null, 500, System.Threading.Timeout.Infinite);
                }

                using var watcherCs = new FileSystemWatcher(root)
                {
                    Filter = "*.cs",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                using var watcherXaml = new FileSystemWatcher(root)
                {
                    Filter = "*.xaml",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };

                watcherCs.Changed += OnChanged;
                watcherCs.Created += OnChanged;
                watcherCs.Deleted += OnChanged;
                watcherCs.Renamed += (s, e) => OnChanged(s, e);

                watcherXaml.Changed += OnChanged;
                watcherXaml.Created += OnChanged;
                watcherXaml.Deleted += OnChanged;
                watcherXaml.Renamed += (s, e) => OnChanged(s, e);

                try { await Task.Delay(System.Threading.Timeout.Infinite, cts.Token); }
                catch (OperationCanceledException) { }

                debounceTimer?.Dispose();
            }

        }, pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption, watchOption);

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
