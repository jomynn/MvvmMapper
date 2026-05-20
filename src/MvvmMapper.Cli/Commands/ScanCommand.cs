using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;

namespace MvvmMapper.Cli.Commands;

internal static class ScanCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public static Command Build(ILoggerFactory loggerFactory)
    {
        var pathArg = new Argument<string>("path", "Path to .sln, .csproj, or folder to scan");
        var outputOption = new Option<string>("--output", () => "./mvvm-map-output", "Output directory");
        var formatOption = new Option<string>("--format", () => "all", "Output format: html|md|json|all");
        var confidenceOption = new Option<string>("--confidence", () => "low", "Minimum confidence to include: high|medium|low");
        var configOption = new Option<string?>("--config", () => null, "Path to mvvm-map.json");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

        var cmd = new Command("scan", "Scan a WPF MVVM solution and generate a relationship map")
        {
            pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption
        };

        cmd.SetHandler((path, output, format, confidence, config, verbose) =>
        {
            _ = output;
            _ = format;
            _ = confidence;
            _ = verbose;

            var fs = new SystemFileSystem();
            var configLoader = new ConfigLoader(fs, loggerFactory.CreateLogger<ConfigLoader>());
            var mvvmConfig = configLoader.Load(config);

            var solutionLoader = new SolutionLoader(loggerFactory.CreateLogger<SolutionLoader>());
            var root = solutionLoader.ResolveRootDirectory(path);

            var discoverer = new FileDiscoverer(fs, loggerFactory.CreateLogger<FileDiscoverer>());
            var discovery = discoverer.Discover(root, mvvmConfig);

            var result = new
            {
                RootDirectory = discovery.RootDirectory,
                XamlFiles = discovery.XamlFiles,
                CsFiles = discovery.CsFiles
            };

            Console.WriteLine(JsonSerializer.Serialize(result, s_jsonOptions));
        }, pathArg, outputOption, formatOption, confidenceOption, configOption, verboseOption);

        return cmd;
    }
}
