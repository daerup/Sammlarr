using Domain.Providers;

namespace Domain.Auth;

public interface ICredential
{
	WatchlistProvider Provider { get; }

	Guid ClientId { get; }

	string AccessToken { get; }

	DateTimeOffset ExpiresAt { get; }
}
