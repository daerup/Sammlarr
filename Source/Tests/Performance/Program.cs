using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Performance;
using Performance.Runners;
using Performance.Runners.Benchmark;
using Performance.Runners.Generate;
using Shared.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false).AddEnvironmentVariables();

builder.Services.AddGeneratedOptions();
builder.Services.AddKeyedTransient<IPerformanceRunner, GenerateModeRunner>(RunningMode.Generate);
builder.Services.AddKeyedTransient<IPerformanceRunner, BenchmarkModeRunner>(RunningMode.Benchmark);

using var host = builder.Build();
var arguments = ParseArguments(args);
var mode = ExtractMode(arguments);
var runner = host.Services.GetRequiredKeyedService<IPerformanceRunner>(mode);
await runner.RunAsync();
return 0;

static Dictionary<string, string> ParseArguments(string[] args) =>
	Enumerable.Range(0, args.Length / 2).ToDictionary(i => args[i * 2].TrimStart('-'), i => args[i * 2 + 1], StringComparer.OrdinalIgnoreCase);

static RunningMode ExtractMode(Dictionary<string, string> arguments)
{
	if (arguments.TryGetValue("mode", out var raw) && Enum.TryParse(raw, true, out RunningMode mode))
	{
		return mode;
	}
	throw new ArgumentException("The 'mode' argument is required and must be either 'Generate' or 'Benchmark'.", nameof(arguments));
}
