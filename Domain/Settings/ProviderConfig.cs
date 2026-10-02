using Domain.Providers;

namespace Domain.Settings;

public record ProviderConfig
{
	public required SinkKind Sink { get; init; }
	public required TrendingProvider TrendingProvider { get; init; }
	public required SearchProvider SearchProvider { get; init; }
	public required AuthProvider AuthProvider { get; init; }
	public required RegistrationMode RegistrationMode { get; init; }
}
