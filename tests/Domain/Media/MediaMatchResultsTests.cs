using Domain.Media;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class MediaMatchResultsTests
{
	private static readonly IExternalMediaId Imdb = ImdbId.From("tt0137523", MediaType.Movie);

	[Test]
	public async Task Externals_WhenMatchedWithPageLinks_AddTheLinksToTheKeys()
	{
		var movie = MediaFakes.CompleteMovie();
		var page = RottenTomatoesId.From("m/fight_club", MediaType.Movie);
		MediaMatchResult match = movie with { Details = movie.Details with { PageLinks = [page] } };

		IExternalMediaId[] expected = [.. movie.Identity.Keys, page];
		await Assert.That(match.Externals).IsEquivalentTo(expected);
	}

	[Test]
	public async Task Externals_WhenUnmatchedWithPageLinks_AddTheLinksToTheExternals()
	{
		var page = MetacriticId.From("fight-club", MediaType.Movie);
		MediaMatchResult match = new UnmatchedMedia([Imdb], new MovieDetails { PageLinks = [page] });

		IExternalMediaId[] expected = [Imdb, page];
		await Assert.That(match.Externals).IsEquivalentTo(expected);
	}

	[Test]
	public async Task Matched_OverMixedResults_KeepsOnlyTheMatchedInOrder()
	{
		var first = MediaFakes.CompleteMovie();
		var second = MediaFakes.CompleteMovie();
		IReadOnlyList<MediaMatchResult> results = [first, new UnmatchedMedia([Imdb], new MovieDetails()), second];

		await Assert.That(results.Matched()).IsEquivalentTo([first, second], CollectionOrdering.Matching);
	}
}
