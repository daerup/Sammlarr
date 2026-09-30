using System.ComponentModel;

namespace Domain.Providers;

public enum SinkKind
{
	[Description("Plex")]
	Plex,
}

public static class SinkKindConversions
{
	public static MediaProvider AsMediaProvider(this SinkKind value) =>
		value switch
		{
			SinkKind.Plex => MediaProvider.Plex,
		};
}
