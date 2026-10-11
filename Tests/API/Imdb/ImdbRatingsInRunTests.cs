using API.Imdb;
using API.Tests.Anime;
using Contracts.Datasets;
using Domain.Media;
using TestInfrastructure;

namespace API.Tests.Imdb;

public class ImdbRatingsInRunTests
{
	[Test]
	public async Task PrimeAsync_ForABurst_ReadsTheRatingsInOneQuery()
	{
		var ratings = IImdbRatings.Mock();
		ratings.FindManyAsync(Any(), Any()).Returns(new Dictionary<string, ImdbRating> { ["tt0137523"] = new(8.8f, Some.Of(2_000_000)) });
		var testee = new ImdbRatingsInRun(ratings.Object, AnimeMaps.Loads(DatasetNames.ImdbRatings).Object);
		await testee.PrimeAsync(
			[
				[ImdbId.From("tt0137523", MediaType.Movie)],
				[ImdbId.From("tt1375666", MediaType.Movie)],
			],
			CancellationToken.None
		);

		var fightClub = await testee.FindAsync("tt0137523", CancellationToken.None);
		var inception = await testee.FindAsync("tt1375666", CancellationToken.None);

		var rated = await Assert.That(fightClub).IsSome();
		await Assert.That(rated!).IsSome().And.IsEqualTo(new ImdbRating(8.8f, Some.Of(2_000_000)));
		var unrated = await Assert.That(inception).IsSome();
		await Assert.That(unrated!).IsNone();
		ratings.FindManyAsync(Any(), Any()).WasCalled(Times.Once);
		ratings.FindAsync(Any(), Any()).WasCalled(Times.Never);
	}
}
