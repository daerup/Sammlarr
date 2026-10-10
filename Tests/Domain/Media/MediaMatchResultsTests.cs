using Domain.Media;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class MediaMatchResultsTests
{
	private static readonly IExternalMediaId Imdb = ImdbId.From("tt0137523", MediaType.Movie);

	[Test]
	public async Task Details_WhenMatched_AreTheMatchedDetails()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaMatchResult match = movie;

		await Assert.That(match.Details).IsSameReferenceAs(movie.Details);
	}

	[Test]
	public async Task Details_WhenUnmatched_AreTheUnmatchedDetails()
	{
		var details = new ShowDetails { Title = "Severance" };
		MediaMatchResult match = new UnmatchedMedia([Imdb], details);

		await Assert.That(match.Details).IsSameReferenceAs(details);
	}

	[Test]
	public async Task Externals_WhenMatched_AreTheIdentityExternals()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaMatchResult match = movie;

		await Assert.That(match.Externals).IsEquivalentTo(movie.Identity.Keys);
	}

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
	public async Task Externals_WhenUnmatched_AreTheExternalsItWasFoundBy()
	{
		MediaMatchResult match = new UnmatchedMedia([Imdb], new MovieDetails());

		await Assert.That(match.Externals).IsEquivalentTo([Imdb]);
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
