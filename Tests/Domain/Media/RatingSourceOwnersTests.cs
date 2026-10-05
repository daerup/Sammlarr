using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class RatingSourceOwnersTests
{
	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	[Test]
	public async Task PublishesVotes_ForEverySource_IsExactlyWhetherItsRatingKeepsVotes()
	{
		await Assert.That(Sources.Where(source => source.PublishesVotes() != source.Rate(5, Some.Of(1)).Votes.IsSome())).IsEmpty();
	}

	[Test]
	public async Task OwnedRatingSources_ForEveryProvider_AreTheSourcesThatNameItTheirOwner()
	{
		await Assert
			.That(Enum.GetValues<MediaProvider>().Where(owner => !owner.OwnedRatingSources().SetEquals(Sources.Where(source => source.Owner() == owner))))
			.IsEmpty();
	}

	[Test]
	public async Task Owner_OfEachSource_IsTheProviderWhoseRatingItIs()
	{
		await Assert
			.That(Sources.Select(source => source.Owner()))
			.IsEquivalentTo([MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.RottenTomatoes, MediaProvider.RottenTomatoes], CollectionOrdering.Matching);
	}
}
