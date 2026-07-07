using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaLookupBridgesTests
{
	[Test]
	public async Task AsIdentified_WhenIdentified_ReturnsInnerMedia()
	{
		MediaLookup match = MediaFakes.CompleteMovie().ToMatch();

		var identified = match.AsIdentified();

		await Assert.That(identified is not null).IsTrue();
		await Assert.That(identified!.Value is Movie).IsTrue();
	}

	[Test]
	public async Task AsIdentified_WhenUnmatched_ReturnsNull()
	{
		MediaLookup match = new UnmatchedMedia { Externals = [TmdbId.From("1", MediaType.Movie)] };

		await Assert.That(match.AsIdentified() is null).IsTrue();
	}

	[Test]
	public async Task ToIdentified_WhenIdentified_IsSome()
	{
		MediaLookup match = MediaFakes.CompleteMovie().ToMatch();

		await Assert.That(match.ToIdentified()).IsSome().And.Multiple(async identified => await Assert.That(identified is Movie).IsTrue());
	}

	[Test]
	public async Task ToIdentified_WhenUnmatched_IsNone()
	{
		MediaLookup match = new UnmatchedMedia { Externals = [TmdbId.From("1", MediaType.Movie)] };

		await Assert.That(match.ToIdentified()).IsNone();
	}

	[Test]
	public async Task ToMatch_RoundTripsThroughAsIdentified()
	{
		var movie = MediaFakes.CompleteMovie();
		IdentifiedMedia identified = movie;

		var roundTripped = identified.ToMatch().AsIdentified();

		await Assert.That(roundTripped is not null).IsTrue();
		await Assert.That(roundTripped!.Value is Movie restored && ReferenceEquals(restored, movie)).IsTrue();
	}

	[Test]
	public async Task Identity_IsTotalForPartial()
	{
		var identity = MediaFakes.MovieIdentity();
		MediaLookup match = new PartialMovie { Identity = identity }.ToMatch();

		await Assert.That(match.Identity.Id).IsEqualTo(identity.Id);
	}
}
