namespace Shared.Options;

public record MdblistOptions : IOptionsSection
{
	public static string SectionName => "MDBList";

	public required string BaseUrl { get; set; }
}
