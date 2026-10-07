namespace Shared.Options;

public record MdblistOptions : IOptionsSection, IProviderHttpOptions
{
	public required string BaseUrl { get; set; }
	public ProviderHttpOptions Http { get; set; } = new();
	public int FeedReserve { get; set; } = 120;
	public static string SectionName => "MDBList";
}
