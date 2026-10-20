using API.Feeds;
using CsCheck;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace API.Tests.Feeds;

public class SiteLinkTests
{
	private static readonly HashSet<string> AiringKeys = ["airing status", "year range"];

	[Test]
	[Arguments("https://anilist.co/search/anime?airing%20status=RELEASING&year%20range=2010&year%20range=2040")]
	[Arguments("http://www.anilist.co/search/anime/?airing+status=RELEASING&year+range=2010&year+range=2040")]
	[Arguments("anilist.co/search/anime?airing status=RELEASING&year range=2010&year range=2040#top")]
	[Arguments("search/anime?airing%20status=RELEASING&year%20range=2010&year%20range=2040")]
	public async Task Parse_ForEveryWayALinkIsPasted_GivesOneStoredForm(string input)
	{
		var link = SiteLink.Parse(input, "anilist.co").Map(found => found.Stored());

		await Assert.That(link).IsSome().And.IsEqualTo("search/anime?airing%20status=RELEASING&year%20range=2010&year%20range=2040");
	}

	[Test]
	public async Task Stored_EscapesEveryReservedCharacterTheWayStoredLinksAlreadyRead()
	{
		var link = SiteLink.Parse("https://www.themoviedb.org/discover/movie?with_genres=28,12&q=a!b(c)*;@$'", "themoviedb.org");

		await Assert.That(link.Map(found => found.Stored())).IsSome().And.IsEqualTo("discover/movie?with_genres=28%2C12&q=a%21b%28c%29%2A%3B%40%24%27");
	}

	[Test]
	public async Task Parse_ForEmptySegmentsBareKeysAndBrokenEscapes_KeepsWhatItCanRead()
	{
		var link = SiteLink.Parse("search/anime?a&&b=%zz&c=x+y", "anilist.co");

		await Assert.That(link.Map(found => found.Stored())).IsSome().And.IsEqualTo("search/anime?a=&b=%25zz&c=x%20y");
	}

	[Test]
	public async Task Parse_DropsTrackingKeys()
	{
		var link = SiteLink.Parse("https://myanimelist.net/topanime.php?_location=mal_h_m&type=airing&utm_source=x", "myanimelist.net");

		await Assert.That(link.Map(found => found.Stored())).IsSome().And.IsEqualTo("topanime.php?type=airing");
	}

	[Test]
	[Arguments("https://myanimelist.net/topanime.php")]
	[Arguments("myanimelist.net/topanime.php")]
	[Arguments("www.themoviedb.org/movie")]
	public async Task Parse_ForAnotherSite_IsNone(string input)
	{
		await Assert.That(SiteLink.Parse(input, "anilist.co")).IsNone();
	}

	[Test]
	public async Task Values_KeepsTheRepeatedKeyInPastedOrder()
	{
		var link = SiteLink.Parse("anilist.co/search/anime?year%20range=2010&genres=Action&year%20range=2040", "anilist.co");

		await Assert.That(link.Map(found => found.Values("year range"))).IsSome().And.IsEquivalentTo(["2010", "2040"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Knowing_EveryKey_KeepsTheLink()
	{
		var link = SiteLink.Parse("search/anime?airing%20status=RELEASING", "anilist.co").GetValueOr(() => throw new InvalidOperationException());

		await Assert.That(link.Knowing(AiringKeys, "AniList")).IsSuccess();
	}

	[Test]
	public async Task Knowing_AnUnknownKey_IsAnErrorNamingIt()
	{
		var link = SiteLink.Parse("search/anime?streaming%20on=Netflix", "anilist.co").GetValueOr(() => throw new InvalidOperationException());

		await Assert
			.That(link.Knowing(AiringKeys, "AniList"))
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).Contains("AniList's streaming on filter"));
	}

	[Test]
	public async Task Knowing_ADroppedKey_LeavesItOut()
	{
		var link = SiteLink.Parse("movie/now-playing?view=calendar", "themoviedb.org").GetValueOr(() => throw new InvalidOperationException());

		await Assert
			.That(link.Knowing(new HashSet<string>(), new HashSet<string> { "view" }, "TMDB").Map(kept => kept.Stored()))
			.IsSuccess()
			.And.IsEqualTo("movie/now-playing");
	}

	[Test]
	public void Parse_OfAStoredForm_GivesItBackUnchanged()
	{
		var segments = Gen.String[Gen.Char['a', 'z'], 1, 8];
		var pairs = Gen.Select(Gen.String[Gen.Char[' ', '~'], 1, 10], Gen.String[Gen.Char[' ', '~'], 0, 10]).List[0, 4];
		Gen.Select(segments.List[1, 3], pairs)
			.Sample(
				sample =>
				{
					var (path, query) = sample;
					var stored = new SiteLink(
						string.Join("/", path),
						[
							.. query
								.Where(pair => !pair.Item1.StartsWith("utm_", StringComparison.Ordinal) && pair.Item1 != "_location")
								.Select(pair => KeyValuePair.Create(pair.Item1, pair.Item2)),
						]
					).Stored();
					return SiteLink.Parse(stored, "anilist.co").Map(found => found.Stored()).GetValueOr(string.Empty) == stored;
				},
				iter: 5_000
			);
	}
}
