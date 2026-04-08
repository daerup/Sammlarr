using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Performance.Options;

namespace Performance.Runners.Benchmark;

internal class BenchmarkModeRunner(BenchmarkModeOptions options) : IPerformanceRunner
{
	public Task RunAsync()
	{
		var fullPath = Path.GetFullPath(options.DbPath);
		MediaIdGroupStoreGetByIdBenchmark.ConfiguredDbPath = fullPath;
		MediaIdGroupStoreGetByIdBenchmark.ConfiguredSeed = options.Seed;

		var config = ManualConfig
			.CreateEmpty()
			.AddColumnProvider(DefaultColumnProviders.Instance)
			.AddLogger(ConsoleLogger.Default)
			.AddDiagnoser(MemoryDiagnoser.Default)
			.AddJob(Job.Default.WithIterationCount(100));
		config.ArtifactsPath = Path.GetDirectoryName(fullPath)!;
		BenchmarkRunner.Run<MediaIdGroupStoreGetByIdBenchmark>(config);
		return Task.CompletedTask;
	}
}
