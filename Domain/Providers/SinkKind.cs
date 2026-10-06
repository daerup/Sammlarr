using System.ComponentModel;

namespace Domain.Providers;

public enum SinkKind
{
	[Description("Plex")]
	Plex,

	[Description("Radarr + Sonarr")]
	Arr,
}

public static class SinkKinds
{
	extension(SinkKind sink)
	{
		public Service Service
		{
			get
			{
				return sink switch
				{
					SinkKind.Plex => RemoteService.Plex,
					SinkKind.Arr => new Arr(),
				};
			}
		}
	}
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
}
