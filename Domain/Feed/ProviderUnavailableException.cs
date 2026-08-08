using Domain.Providers;

namespace Domain.Feed;

public sealed class ProviderUnavailableException(FeedProvider provider, Exception? innerException = null)
	: Exception($"The {provider} provider is temporarily unavailable.", innerException)
{
	public FeedProvider Provider { get; } = provider;
}
