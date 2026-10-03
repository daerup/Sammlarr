using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaLookupBridgesTests
{
	[Test]
	public async Task AsIdentified_WhenIdentified_ReturnsInnerMedia()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaLookup match = movie;

		await Assert.That(match.AsIdentified()).IsSameReferenceAs(movie);
	}

	[Test]
	public async Task AsIdentified_WhenUnmatched_ReturnsNull()
	{
		MediaLookup match = new UnmatchedMedia([TmdbId.From("1", MediaType.Movie)], new MovieDetails());

		await Assert.That(match.AsIdentified()).IsNull();
	}

	[Test]
	public async Task ToIdentified_WhenIdentified_IsSome()
	{
		var movie = MediaFakes.CompleteMovie();
		MediaLookup match = movie;

		await Assert.That(match.ToIdentified()).IsSome().And.Multiple(async identified => await Assert.That(identified).IsSameReferenceAs(movie));
	}

	[Test]
	public async Task ToIdentified_WhenUnmatched_IsNone()
	{
		MediaLookup match = new UnmatchedMedia([TmdbId.From("1", MediaType.Movie)], new MovieDetails());

		await Assert.That(match.ToIdentified()).IsNone();
	}

	[Test]
	public async Task ToMatch_RoundTripsThroughAsIdentified()
	{
		var movie = MediaFakes.CompleteMovie();

		var roundTripped = movie.ToMatch().AsIdentified();

		await Assert.That(roundTripped).IsSameReferenceAs(movie);
	}

	[Test]
	public async Task Identity_WhenIdentifiedWithEmptyDetails_IsTheIdentity()
	{
		var identity = MediaFakes.MovieIdentity();
		MediaLookup match = new IdentifiedMedia(identity, new MovieDetails());

		await Assert.That(match.Identity.Id).IsEqualTo(identity.Id);
	}
}
