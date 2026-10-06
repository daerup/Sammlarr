using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class RatingCardTests
{
	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	[Test]
	public async Task Owner_OfEachSource_IsTheProviderWhoseRatingItIs()
	{
		await Assert
			.That(Sources.Select(source => source.Card.Owner))
			.IsEquivalentTo([MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.RottenTomatoes, MediaProvider.RottenTomatoes], CollectionOrdering.Matching);
	}

	[Test]
	public async Task PublishesVotes_ForEverySource_IsExactlyWhetherItsRatingKeepsVotes()
	{
		await Assert.That(Sources.Where(source => source.Card.PublishesVotes != source.Card.Rate(5, Some.Of(1)).Votes.IsSome())).IsEmpty();
	}

	[Test]
	public async Task Rate_ForEverySource_IsARatingOfThatSource()
	{
		await Assert.That(Sources.Where(source => source.Card.Rate(5, new None()).Source != source)).IsEmpty();
	}
}
