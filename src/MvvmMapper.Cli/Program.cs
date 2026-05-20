using System.CommandLine;
using Microsoft.Extensions.Logging;
using MvvmMapper.Cli.Commands;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var rootCommand = new RootCommand("mvvm-map: offline WPF MVVM static analysis tool");
rootCommand.AddCommand(ScanCommand.Build(loggerFactory));
rootCommand.AddCommand(InitCommand.Build());

return await rootCommand.InvokeAsync(args);
