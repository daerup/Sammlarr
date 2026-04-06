using System.ComponentModel.DataAnnotations;
using Shared.Options;

namespace Performance.Options;

internal class GenerateModeOptions : IOptionsSection
{
	public static string SectionName => "Generate";

	public string DbPath { get; set; } = null!;

	[Range(1, int.MaxValue)]
	public int EntryCount { get; set; }
	public int BatchSize { get; set; }

	public int Seed { get; set; }

	public bool Overwrite { get; set; }
}
