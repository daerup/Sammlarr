namespace Shared.Options;

public record ArrOptions : IOptionsSection, IProviderHttpOptions
{
	public ProviderHttpOptions Http { get; set; } = new();
	public static string SectionName => "Arr";
}
