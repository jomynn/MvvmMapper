using System.CommandLine;
using System.Text.Json;
using MvvmMapper.Core.Configuration;

namespace MvvmMapper.Cli.Commands;

internal static class InitCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public static Command Build()
    {
        var cmd = new Command("init", "Write a default mvvm-map.json to the current directory");

        cmd.SetHandler(() =>
        {
            const string path = "mvvm-map.json";
            if (File.Exists(path))
            {
                Console.WriteLine($"mvvm-map.json already exists at {Path.GetFullPath(path)}");
                return;
            }

            var config = new MvvmMapConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(config, s_jsonOptions));
            Console.WriteLine($"Written default config to {Path.GetFullPath(path)}");
        });

        return cmd;
    }
}
