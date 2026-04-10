using Contracts.Auth;
using Contracts.Media;

namespace Contracts.Settings;

public record ProviderSettings
{
	public required AuthProvider WatchlistProvider { get; init; }

	public required MediaProvider TrendingProvider { get; init; }

	public required MediaProvider SearchProvider { get; init; }
}
