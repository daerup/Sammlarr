namespace Shared.Options;

public record MdblistOptions : IOptionsSection
{
	public required string BaseUrl { get; set; }
	public static string SectionName => "MDBList";
}
