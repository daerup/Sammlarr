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
				Help(
					"Paste the list's link from mdblist.com, or its owner/list, e.g. <code>snoak/trakt-s-trending-shows</code>. Its number works too.",
					"https://mdblist.com/toplists/",
					"Browse lists on MDBList"
				),
				identifier => $"https://mdblist.com/lists/{identifier}"
			),
			FeedProvider.Tmdb => new(
				"e.g. https://www.themoviedb.org/list/8231234",
				Help(
					"Paste the list's link from themoviedb.org, or its number, e.g. <code>8231234</code>. "
						+ "A list only TMDB's v4 API serves needs the read access token under TMDB's key in Settings › Providers.",
					"https://www.themoviedb.org/list/new",
					"Make a list on TMDB"
				),
				identifier => $"https://www.themoviedb.org/list/{identifier}"
			),
			FeedProvider.AniList => new(
				"e.g. https://anilist.co/user/yourname/animelist/Planning",
				Help(
					"Paste an AniList username, username/status (current, planning, completed, dropped, paused or repeating), or the list's anilist.co link. The list must be public.",
					"https://anilist.co/search/users",
					"Find a user on AniList"
				),
				identifier => $"https://anilist.co/user/{identifier.Split('/')[0]}/animelist"
			),
			FeedProvider.Mal => new(
				"e.g. https://myanimelist.net/animelist/yourname",
				Help(
					"Paste a MyAnimeList username or the list's myanimelist.net/animelist link. The list must be public.",
					"https://myanimelist.net/users.php",
					"Find a user on MyAnimeList"
				),
				identifier => $"https://myanimelist.net/animelist/{identifier}"
			),
		};
	}

	private static MarkupString Help(string text, string browse, string label)
	{
		return new($"{text} <a href=\"{browse}\" target=\"_blank\" class=\"underline underline-offset-2 hover:text-foreground transition-colors\">{label}</a>");
	}
}
