using Domain.Providers;

namespace UI.Shared.Media;

internal static class ListTexts
{
	public const string EmptyBlocklist = "Feeds never add a blocklisted title. Anything already on your wantlist stays there.";

	public static string EmptyWantlist(SinkKind sink)
	{
		return sink switch
		{
			SinkKind.Plex =>
				"Titles you add here, and titles your feeds find, go to your Plex watchlist on the next sync. Titles you add on Plex come back the same way.",
			SinkKind.Arr => "Titles you add here, and titles your feeds find, go to Radarr and Sonarr on the next sync. Titles you tag there come back the same way.",
		};
	}
}
