using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Configuration;

namespace MvvmMapper.Core.Discovery;

public sealed record DiscoveryResult(
    IReadOnlyList<string> XamlFiles,
    IReadOnlyList<string> CsFiles,
    string RootDirectory);

public sealed class FileDiscoverer
{
    private readonly IFileSystem _fs;
    private readonly ILogger<FileDiscoverer> _logger;

    public FileDiscoverer(IFileSystem fs, ILogger<FileDiscoverer> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public DiscoveryResult Discover(string rootDirectory, MvvmMapConfig config)
    {
        _logger.LogInformation("Discovering files in {Root}", rootDirectory);

        var excludePatterns = config.Exclude;

        var xamlFiles = _fs
            .EnumerateFiles(rootDirectory, "*.xaml", System.IO.SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f, excludePatterns))
            .OrderBy(f => f)
            .ToList();

        var csFiles = _fs
            .EnumerateFiles(rootDirectory, "*.cs", System.IO.SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f, excludePatterns))
            .OrderBy(f => f)
            .ToList();

        _logger.LogInformation("Found {XamlCount} XAML files and {CsCount} C# files", xamlFiles.Count, csFiles.Count);

        return new DiscoveryResult(xamlFiles, csFiles, rootDirectory);
    }

    private static bool IsExcluded(string path, string[] patterns)
    {
        var normalized = path.Replace('\\', '/');
        return patterns.Any(pattern =>
        {
            // Extract the meaningful segment from glob patterns like **/bin/** or **/obj/**
            // Strip leading **/ and trailing /** to get the directory segment
            var segment = pattern.Replace('\\', '/');
            segment = segment.TrimStart('*', '/');
            segment = segment.TrimEnd('*', '/');
            if (string.IsNullOrEmpty(segment)) return false;

            // Match if the segment appears as a path component
            return normalized.Contains('/' + segment + '/', StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith('/' + segment, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(segment + '/', StringComparison.OrdinalIgnoreCase);
        });
    }
}
