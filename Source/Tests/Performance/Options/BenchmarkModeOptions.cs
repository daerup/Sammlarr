using Shared.Options;

namespace Performance.Options;

internal class BenchmarkModeOptions : IOptionsSection
{
	public static string SectionName => "Benchmark";
	public string DbPath { get; set; }
	public int Seed { get; set; } = 1337;
}
