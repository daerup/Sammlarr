using API.Feeds;
using API.Mal;
using TestInfrastructure;

namespace API.Tests.Mal;

public class MalPagesTests
{
	private static readonly DateOnly Today = new(2026, 10, 6);

	private const string Fields = "&fields=media_type,start_date,alternative_titles";

	[Test]
	[Arguments("topanime.php", "anime/ranking?ranking_type=all&limit=500&offset=0" + Fields)]
	[Arguments("topanime.php?type=upcoming", "anime/ranking?ranking_type=upcoming&limit=500&offset=0" + Fields)]
	[Arguments("topanime.php?type=bypopularity&limit=50", "anime/ranking?ranking_type=bypopularity&limit=500&offset=50" + Fields)]
	[Arguments("topanime.php?type=favorite", "anime/ranking?ranking_type=favorite&limit=500&offset=0" + Fields)]
	[Arguments("anime/season", "anime/season/2026/fall?sort=anime_num_list_users&limit=500" + Fields)]
	[Arguments("anime/season/schedule", "anime/season/2026/fall?sort=anime_num_list_users&limit=500" + Fields)]
	[Arguments("anime/season/2026/summer", "anime/season/2026/summer?sort=anime_num_list_users&limit=500" + Fields)]
	public async Task Request_ForEachPage_AsksTheApiForTheSameTitles(string stored, string request)
	{
		var page = MalPages.Of(Page(stored)).Map(found => MalPages.Request(found, Today));

		await Assert.That(page).IsSuccess().And.IsEqualTo(request);
	}

	[Test]
	[Arguments("topanime.php?type=ona", "ona")]
	[Arguments("topanime.php?type=music", "music")]
	[Arguments("topanime.php?limit=lots", "lots")]
	[Arguments("anime.php?q=frieren&cat=anime", "search")]
	[Arguments("anime/season/later", "anime/season/later")]
	[Arguments("anime/season/archive", "anime/season/archive")]
	[Arguments("anime/genre/1/Action", "anime/genre/1/Action")]
	[Arguments("anime/producer/569/MAPPA", "anime/producer/569/MAPPA")]
	public async Task Of_APageOrValueItCannotFollow_IsAnErrorNamingIt(string stored, string named)
	{
		await Assert.That(MalPages.Of(Page(stored))).IsError().And.Multiple(async error => await Assert.That(error.Reason).Contains(named));
	}

	private static SiteLink Page(string stored) => SiteLink.Parse(stored, "myanimelist.net").GetValueOr(() => throw new InvalidOperationException(stored));
}
