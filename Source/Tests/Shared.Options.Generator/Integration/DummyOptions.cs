namespace Shared.Options.Generator.Tests.Integration;

internal sealed record DummyOptions : IOptionsSection
{
	public string? BaseUrl { get; set; }

	public bool Enabled { get; set; }
	public static string SectionName => "Dummy";
}
