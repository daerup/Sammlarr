using Performance.Options;

namespace Performance.Runners.Generate;

internal sealed class GenerateModeRunner(GenerateModeOptions options) : IPerformanceRunner
{
	public async Task RunAsync()
	{
		var generator = new MediaIdDatasetGenerator(options);
		await generator.GenerateAsync();
	}
}
