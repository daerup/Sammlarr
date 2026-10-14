using Domain.Media.Conversions;
using Domain.Providers;

namespace Domain.Tests.Media.Conversions;

public class IdLookupTests
{
	private static readonly IdLookup Detail = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);

	[Test]
	public async Task Equals_ForTwoLookupsDeclaredAlike_HoldsAndTheyHashAlike()
	{
		IdLookup alike = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb, MediaProvider.Imdb], Cost: 1);

		await Assert.That(alike).IsEqualTo(Detail);
		await Assert.That(alike.GetHashCode()).IsEqualTo(Detail.GetHashCode());
	}
}
