using Shared.Options;

namespace Performance.Options;

internal class BenchmarkModeOptions : IOptionsSection
{
	public string DbPath { get; set; }
	public int Seed { get; set; } = 1337;
	public static string SectionName => "Benchmark";
}
