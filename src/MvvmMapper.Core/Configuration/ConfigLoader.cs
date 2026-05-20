using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MvvmMapper.Core.Configuration;

public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);
    private readonly IFileSystem _fs;
    private readonly ILogger<ConfigLoader> _logger;

    public ConfigLoader(IFileSystem fs, ILogger<ConfigLoader> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public MvvmMapConfig Load(string? configPath)
    {
        var path = configPath ?? "mvvm-map.json";
        if (!_fs.FileExists(path))
        {
            _logger.LogDebug("Config file not found at {Path}, using defaults", path);
            return new MvvmMapConfig();
        }

        try
        {
            var json = _fs.ReadAllText(path);
            return JsonSerializer.Deserialize<MvvmMapConfig>(json, s_options) ?? new MvvmMapConfig();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse config at {Path}, using defaults", path);
            return new MvvmMapConfig();
        }
    }
}
