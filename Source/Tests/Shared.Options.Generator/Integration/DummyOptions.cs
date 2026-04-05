namespace Shared.Options.Generator.Tests.Integration;

internal sealed record DummyOptions : IOptionsSection
{
	public static string SectionName => "Dummy";

	public string? BaseUrl { get; set; }

	public bool Enabled { get; set; }
}
