using System.ComponentModel;

namespace Domain.Providers;

public enum SinkKind
{
	[Description("Plex")]
	Plex,

	[Description("Radarr + Sonarr")]
	Arr,
}

public static class SinkKindConversions
{
	public static Optional<MediaProvider> Provider(this SinkKind value)
	{
		return value switch
		{
			SinkKind.Plex => Some.Of(MediaProvider.Plex),
			SinkKind.Arr => new None(),
		};
	}

	public static IReadOnlyList<MediaProvider> Providers(this SinkKind value)
	{
		return value switch
		{
			SinkKind.Plex => [MediaProvider.Plex],
			SinkKind.Arr => [MediaProvider.Radarr, MediaProvider.Sonarr],
		};
	}
}
