using Domain.Media.Conversions;
using Domain.Providers;

namespace Domain.Tests.Media.Conversions;

public class IdLookupTests
{
	private static readonly IdLookup Detail = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);

	private static readonly IdLookup Search = new ByHints(Yields: [MediaProvider.Plex, MediaProvider.Tmdb], Cost: 4);

	[Test]
	public async Task Yields_OfALookupById_AreTheIdsItsAnswerMayContain()
	{
		await Assert.That(Detail.Yields).IsEquivalentTo([MediaProvider.Imdb, MediaProvider.Tvdb]);
	}

	[Test]
	public async Task Cost_OfALookupById_IsItsDeclaredCost()
	{
		await Assert.That(Detail.Cost).IsEqualTo(1);
	}

	[Test]
	public async Task Yields_OfALookupByHints_AreTheIdsItsAnswerMayContain()
	{
		await Assert.That(Search.Yields).IsEquivalentTo([MediaProvider.Plex, MediaProvider.Tmdb]);
	}

	[Test]
	public async Task Cost_OfALookupByHints_IsItsDeclaredCost()
	{
		await Assert.That(Search.Cost).IsEqualTo(4);
	}

	[Test]
	public async Task Equals_ForTwoLookupsDeclaredAlike_HoldsAndTheyHashAlike()
	{
		IdLookup alike = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Tvdb, MediaProvider.Imdb], Cost: 1);

		await Assert.That(alike).IsEqualTo(Detail);
		await Assert.That(alike.GetHashCode()).IsEqualTo(Detail.GetHashCode());
	}
}
