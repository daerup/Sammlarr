namespace Domain.Providers;

public sealed class ProviderUnavailableException(MediaProvider provider, Exception? innerException = null)
	: Exception($"The {provider} provider is temporarily unavailable.", innerException)
{
	public MediaProvider Provider { get; } = provider;
}
