using Domain.Providers;

namespace UI.Shared.Services;

internal static class ServiceKeys
{
	private const string ArrKey = "arr";

	private const string AccountsKey = "sammlarr";

	internal static string Key(Service service)
	{
		return service switch
		{
			RemoteService remote => remote.ToString().ToLowerInvariant(),
			Arr => ArrKey,
			SammlarrAccounts => AccountsKey,
		};
	}

	internal static Optional<Service> Parse(string? key)
	{
		return key?.ToLowerInvariant() switch
		{
			null => new None(),
			ArrKey => Some.Of<Service>(new Arr()),
			AccountsKey => Some.Of<Service>(new SammlarrAccounts()),
			var other => Remote(other),
		};
	}

	private static Optional<Service> Remote(string key) =>
		Optional.When(Enum.TryParse<RemoteService>(key, ignoreCase: true, out var remote), () => (Service)remote);
}
