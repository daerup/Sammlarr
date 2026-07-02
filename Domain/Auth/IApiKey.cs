using Domain.Providers;

namespace Domain.Auth;

public interface IApiKey
{
	ApiKeyProvider Provider { get; }

	string Value { get; }
}
