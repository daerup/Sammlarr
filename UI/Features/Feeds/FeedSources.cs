using Domain.Providers;
using Microsoft.AspNetCore.Components;

namespace UI.Features.Feeds;

internal sealed record FeedSource(string Placeholder, MarkupString Help, Func<string, string> Page);

internal static class FeedSources
{
	internal static FeedSource Of(FeedProvider provider)
	{
		return provider switch
		{
			FeedProvider.Mdblist => new(
				"e.g. https://mdblist.com/lists/snoak/trakt-s-trending-shows",
				new MarkupString(
					"Paste the list's link from mdblist.com, or its owner/list, e.g. <code>snoak/trakt-s-trending-shows</code>. Its number works too. "
						+ "<a href=\"https://mdblist.com/toplists/\" target=\"_blank\" "
						+ "class=\"underline underline-offset-2 hover:text-foreground transition-colors\">Browse lists on MDBList</a>"
				),
				identifier => $"https://mdblist.com/lists/{identifier}"
			),
			FeedProvider.Tmdb => new(
				"e.g. https://www.themoviedb.org/list/8231234",
				new MarkupString(
					"Paste the list's link from themoviedb.org, or its number, e.g. <code>8231234</code>. "
						+ "A list only TMDB's v4 API serves needs the read access token under TMDB's key in Settings › Providers."
				),
				identifier => $"https://www.themoviedb.org/list/{identifier}"
			),
			FeedProvider.AniList => new(
				"e.g. https://anilist.co/user/yourname/animelist/Planning",
				new MarkupString(
					"Paste an AniList username, username/status (current, planning, completed, dropped, paused or repeating), or the list's anilist.co link. The list must be public."
				),
				identifier => $"https://anilist.co/user/{identifier.Split('/')[0]}/animelist"
			),
		};
	}
}
