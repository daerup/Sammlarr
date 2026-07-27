using Domain.Providers;

namespace Domain.Auth;

public interface ICredential
{
	AuthProvider Provider { get; }

	Guid ClientId { get; }

	string AccessToken { get; }

	DateTimeOffset ExpiresAt { get; }
}
