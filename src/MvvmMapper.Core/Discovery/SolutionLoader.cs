using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace MvvmMapper.Core.Discovery;

[ExcludeFromCodeCoverage]
public sealed class SolutionLoader
{
    private readonly ILogger<SolutionLoader> _logger;
    private static bool s_msBuildRegistered;
    private static readonly object s_lock = new();

    public SolutionLoader(ILogger<SolutionLoader> logger)
    {
        _logger = logger;
    }

    public async Task<Solution?> TryLoadSolutionAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureMsBuildRegistered();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var workspace = MSBuildWorkspace.Create();

        workspace.WorkspaceFailed += (_, args) =>
            _logger.LogWarning("MSBuild workspace warning: {Message}", args.Diagnostic.Message);

        try
        {
            if (ext == ".sln")
            {
                _logger.LogInformation("Loading solution: {Path}", path);
                return await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken);
            }

            if (ext == ".csproj")
            {
                _logger.LogInformation("Loading project: {Path}", path);
                var project = await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken);
                return project.Solution;
            }

            _logger.LogDebug("Path is a directory, skipping MSBuild load: {Path}", path);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load via MSBuildWorkspace, file discovery will fall back to filesystem scan");
            return null;
        }
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (s_lock)
        {
            if (!s_msBuildRegistered)
            {
                MSBuildLocator.RegisterDefaults();
                s_msBuildRegistered = true;
            }
        }
    }

    public string ResolveRootDirectory(string path)
    {
        if (Directory.Exists(path)) return path;
        if (File.Exists(path)) return Path.GetDirectoryName(path) ?? path;
        throw new ArgumentException($"Path does not exist: {path}", nameof(path));
    }
}
