using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media;

public class MediaValueEqualityTests
{
	[Test]
	public async Task MediaIdentity_WhenSameContentDifferentInstances_AreEqual()
	{
		var id = MediaId.From(Guid.NewGuid());
		var first = new MediaIdentity(id, [TmdbId.From("42", MediaType.Movie)]);
		var second = new MediaIdentity(id, [TmdbId.From("42", MediaType.Movie)]);

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task IdentifiedMedia_WhenSameContentDifferentInstances_AreEqual()
	{
		var first = MediaFakes.CompleteMovie();
		var second = first with
		{
			Identity = first.Identity with { Externals = [.. first.Identity.Externals] },
			Details = first.Details with { Genres = [.. first.Details.Genres], Rating = RatingSummary.From([.. first.Details.Rating.Sources]) },
		};

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task IdentifiedMedia_WhenDifferentGenres_AreNotEqual()
	{
		var first = MediaFakes.CompleteMovie();
		var second = first with { Details = first.Details with { Genres = [.. first.Details.Genres, "Extra Genre"] } };

		await Assert.That(first).IsNotEqualTo(second);
	}

	[Test]
	public async Task MediaDetails_WhenSameFieldsButDifferentTypes_AreNotEqual()
	{
		MediaDetails movie = new MovieDetails { Title = "Dune" };
		MediaDetails show = new ShowDetails { Title = "Dune" };

		await Assert.That(movie).IsNotEqualTo(show);
	}
}
