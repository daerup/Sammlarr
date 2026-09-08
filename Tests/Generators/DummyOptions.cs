using Shared.Options;

namespace Generators.Tests;

internal sealed record DummyOptions : IOptionsSection
{
	public string? BaseUrl { get; set; }

	public bool Enabled { get; set; }
	public static string SectionName => "Dummy";
}
