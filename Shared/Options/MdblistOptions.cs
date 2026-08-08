namespace Shared.Options;

public record MdblistOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "MDBList";
}
