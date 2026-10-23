using Domain.Providers;

namespace Domain.Auth;

public interface IApiKey
{
	ApiKeyProvider Provider { get; }

	Secret Value { get; }
}
