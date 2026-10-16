namespace Shared.Options;

public interface IProviderHttpOptions
{
	ProviderHttpOptions Http { get; }

	IReadOnlyDictionary<string, string> Urls { get; }
}
