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
	public async Task Movie_WhenSameContentDifferentInstances_AreEqual()
	{
		var first = MediaFakes.CompleteMovie();
		var second = first with
		{
			Identity = first.Identity with { Externals = [.. first.Identity.Externals] },
			Genres = [.. first.Genres],
			Rating = RatingSummary.From([.. first.Rating.Sources]),
		};

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Movie_WhenDifferentGenres_AreNotEqual()
	{
		var first = MediaFakes.CompleteMovie();
		var second = first with { Genres = [.. first.Genres, "Extra Genre"] };

		await Assert.That(first).IsNotEqualTo(second);
	}
}
