using System.ComponentModel;

namespace Domain.Providers;

public enum AuthProvider
{
	[Description("Plex")]
	Plex,

	[Description("Sammlarr accounts")]
	Local,
}

public static class AuthProviderConversions
{
	public static Optional<MediaProvider> Provider(this AuthProvider value)
	{
		return value switch
		{
			AuthProvider.Plex => Some.Of(MediaProvider.Plex),
			AuthProvider.Local => new None(),
		};
	}
}
