using API.Mdblist;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistListsTests
{
	[Test]
	[Arguments("https://mdblist.com/lists/snoak/trakt-s-trending-shows", "snoak/trakt-s-trending-shows")]
	[Arguments("https://mdblist.com/lists/snoak/trakt-s-trending-shows/", "snoak/trakt-s-trending-shows")]
	[Arguments("https://www.mdblist.com/lists/snoak/trakt-s-trending-shows?sort=rank", "snoak/trakt-s-trending-shows")]
	[Arguments("mdblist.com/lists/hdlists/latest-hd-documentary-movies-1980-to-today", "hdlists/latest-hd-documentary-movies-1980-to-today")]
	[Arguments("snoak/trakt-s-trending-shows", "snoak/trakt-s-trending-shows")]
	[Arguments(" snoak/trakt-s-trending-shows ", "snoak/trakt-s-trending-shows")]
	[Arguments("88434", "88434")]
	[Arguments("https://mdblist.com/lists/chazlarson/external/2868", "chazlarson/external/2868")]
	[Arguments("chazlarson/external/2868", "chazlarson/external/2868")]
	[Arguments("99999999999", "99999999999")]
	public async Task Parse_ForAListItCanRead_AnswersTheListPart(string input, string list)
	{
		await Assert.That(MdblistLists.Parse(input)).IsSome().And.IsEqualTo(list);
	}

	[Test]
	[Arguments("top-250-movies")]
	[Arguments("https://mdblist.com/lists/snoak")]
	[Arguments("https://www.imdb.com/list/ls055592025/")]
	[Arguments("https://trakt.tv/users/snoak/lists/trending")]
	[Arguments("../user")]
	[Arguments("")]
	public async Task Parse_ForAnythingElse_IsNone(string input)
	{
		await Assert.That(MdblistLists.Parse(input)).IsNone();
	}

	[Test]
	[Arguments("snoak/trakt-s-trending-shows", "lists/snoak/trakt-s-trending-shows/items")]
	[Arguments("88434", "lists/88434/items")]
	[Arguments("chazlarson/external/2868", "external/lists/2868/items")]
	[Arguments("chazlarson/External/2868", "external/lists/2868/items")]
	public async Task ItemsPath_ReadsAnExternalListThroughItsOwnEndpoint(string list, string path)
	{
		await Assert.That(MdblistLists.ItemsPath(list)).IsEqualTo(path);
	}
}
