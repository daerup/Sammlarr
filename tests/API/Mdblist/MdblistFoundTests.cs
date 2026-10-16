using System.Text.Json;
using API.Mdblist;
using API.Mdblist.Types;
using Domain.Media;
using Domain.Providers;

namespace API.Tests.Mdblist;

public class MdblistFoundTests
{
	private static readonly MdblistTypes.ListItems.Ids Ids = new("tt0137523", 550, null, "a5as", null);

	[Test]
	public async Task Of_ForAMetacriticPlaceholder_ReturnsNoLinkAndNoKey()
	{
		var found = MdblistFound.Of(new MdblistTypes.Titles.Title(Ids, [Rating("metacritic", "-")]), MediaType.Movie);

		await Assert.That(found.Details.PageLinks).IsEmpty();
		await Assert.That(found.Externals.Select(id => id.Source)).DoesNotContain(MediaProvider.Metacritic);
	}

	[Test]
	public async Task Of_ForRatingPages_ReturnsThemAsPageLinksAndNotAsKeys()
	{
		var found = MdblistFound.Of(
			new MdblistTypes.Titles.Title(Ids, [Rating("tomatoes", "/m/fight_club"), Rating("metacritic", "/fight-club")]),
			MediaType.Movie
		);

		await Assert.That(found.Details.PageLinks.Select(link => $"{link}")).IsEquivalentTo(["RottenTomatoes:Movie:m/fight_club", "Metacritic:Movie:fight-club"]);
		await Assert.That(found.Externals.Select(id => id.Source)).IsEquivalentTo([MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Mdblist]);
	}

	[Test]
	public async Task Of_ForCriticsAndAudiencePagesThatDiffer_KeepsOneRottenTomatoesLink()
	{
		var found = MdblistFound.Of(
			new MdblistTypes.Titles.Title(Ids, [Rating("tomatoes", "/m/fight_club"), Rating("popcorn", "/m/fight_club_1999")]),
			MediaType.Movie
		);

		await Assert.That(found.Details.PageLinks.Select(link => $"{link}")).IsEquivalentTo(["RottenTomatoes:Movie:m/fight_club"]);
	}

	private static MdblistTypes.Titles.Rating Rating(string source, string url) => new(source, 61, 10, JsonSerializer.SerializeToElement(url));
}
