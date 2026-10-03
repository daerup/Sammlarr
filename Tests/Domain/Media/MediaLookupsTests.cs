using Domain.Media;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class MediaLookupsTests
{
	private static readonly IExternalMediaId Imdb = ImdbId.From("tt0137523", MediaType.Movie);

	[Test]
	public async Task Details_WhenIdentified_AreTheIdentifiedDetails()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaLookup lookup = movie;

		await Assert.That(lookup.Details).IsSameReferenceAs(movie.Details);
	}

	[Test]
	public async Task Details_WhenUnmatched_AreTheUnmatchedDetails()
	{
		var details = new ShowDetails { Title = "Severance" };
		MediaLookup lookup = new UnmatchedMedia([Imdb], details);

		await Assert.That(lookup.Details).IsSameReferenceAs(details);
	}

	[Test]
	public async Task Externals_WhenIdentified_AreTheIdentityExternals()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaLookup lookup = movie;

		await Assert.That(lookup.Externals).IsEquivalentTo(movie.Identity.Externals);
	}

	[Test]
	public async Task Externals_WhenUnmatched_AreTheExternalsItWasFoundBy()
	{
		MediaLookup lookup = new UnmatchedMedia([Imdb], new MovieDetails());

		await Assert.That(lookup.Externals).IsEquivalentTo([Imdb]);
	}

	[Test]
	public async Task Identified_OverMixedLookups_KeepsOnlyTheIdentifiedInOrder()
	{
		var first = MediaFakes.CompleteMovie();
		var second = MediaFakes.CompleteMovie();
		IReadOnlyList<MediaLookup> lookups = [first, new UnmatchedMedia([Imdb], new MovieDetails()), second];

		await Assert.That(lookups.Identified()).IsEquivalentTo([first, second], CollectionOrdering.Matching);
	}
}
