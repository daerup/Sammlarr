using DB.Conversion.Ratings;
using Domain.Media;
using TestInfrastructure;

namespace DB.Tests.Conversion;

public class RatingStrategyConverterTests
{
	private readonly RatingStrategyConverter _testee = new();

	[Test]
	[Arguments("averaged")]
	[Arguments("averaged:Imdb,RottenTomatoesCritics")]
	[Arguments("ordered:Imdb,Tmdb,RottenTomatoesCritics,RottenTomatoesAudience")]
	public async Task Convert_ForEachStoredForm_RoundTripsTheText(string stored)
	{
		var restored = _testee.ConvertToProvider(_testee.ConvertFromProvider(stored));

		await Assert.That(restored).IsEqualTo(stored);
	}

	[Test]
	public async Task ConvertFromProvider_WhenAveragedNamesNoSource_AveragesEverySource()
	{
		var restored = (RatingStrategy)_testee.ConvertFromProvider("averaged")!;

		await Assert.That(restored).IsEqualTo(new RatingStrategy(AveragedRating.Everything));
	}

	[Test]
	public async Task ConvertFromProvider_WhenAStoredOrderMissesASource_PutsItLast()
	{
		var restored = (RatingStrategy)_testee.ConvertFromProvider("ordered:Imdb,Tmdb")!;

		await Assert.That(restored).IsOfUnionType<RatingStrategy, OrderedRating>();
		await Assert.That(_testee.ConvertToProvider(restored)).IsEqualTo("ordered:Imdb,Tmdb,RottenTomatoesCritics,RottenTomatoesAudience");
	}

	[Test]
	public async Task Convert_WhenAveragingSomeSources_RoundTripsTheStrategy()
	{
		var original = new RatingStrategy(new AveragedRating([RatingSource.RottenTomatoesAudience, RatingSource.Tmdb]));

		var restored = (RatingStrategy)_testee.ConvertFromProvider(_testee.ConvertToProvider(original))!;

		await Assert.That(restored).IsEqualTo(original);
	}
}
