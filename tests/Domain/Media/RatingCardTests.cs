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
			.IsEquivalentTo(
				[
					MediaProvider.Tmdb,
					MediaProvider.Imdb,
					MediaProvider.RottenTomatoes,
					MediaProvider.RottenTomatoes,
					MediaProvider.Metacritic,
					MediaProvider.Letterboxd,
					MediaProvider.Trakt,
					MediaProvider.Mal,
					MediaProvider.AniList,
				],
				CollectionOrdering.Matching
			);
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

	[Test]
	[Arguments(RatingSource.Metacritic, 7.3f, "73", 7.3f)]
	[Arguments(RatingSource.Letterboxd, 7.8f, "3.9", 7.8f)]
	[Arguments(RatingSource.Trakt, 8.4f, "84%", 8.4f)]
	[Arguments(RatingSource.MyAnimeList, 8.62f, "8.62", 8.62f)]
	public async Task Rate_ForEachMdblistSource_ReadsInItsOwnScale(RatingSource source, float rated, string shown, float sammlarr)
	{
		var rating = source.Card.Rate(rated, Some.Of(100));

		await Assert.That(rating.Value).IsEqualTo(shown);
		await Assert.That(rating.SammlarrValue).IsEqualTo(sammlarr).Within(0.001f);
	}

	[Test]
	[Arguments(61, RatingVerdict.Liked)]
	[Arguments(39, RatingVerdict.Disliked)]
	public async Task Verdict_OfAMetascoreAtTheBar_FallsOnItsSide(int metascore, RatingVerdict verdict)
	{
		await Assert.That(new MetacriticRating(metascore).Verdict).IsSome().And.IsEqualTo(verdict);
	}

	[Test]
	public async Task Verdict_OfAMixedMetascore_IsNone()
	{
		await Assert.That(new MetacriticRating(50).Verdict).IsNone();
	}
}
