using API.Plex;
using Bogus;
using Contracts.Media;

namespace API.Tests.Plex;

public class PlexWatchlistItemTests
{
	private static readonly Faker<PlexWatchlistItem> watchlistItemFaker = new Faker<PlexWatchlistItem>()
		.RuleFor(item => item.RatingKey, faker => PlexId.From($"plex-{faker.Random.Number(1000, 9999)}"))
		.RuleFor(item => item.Title, faker => faker.Lorem.Sentence(3));

	[Test]
	public async Task SourceId_ReturnsRatingKey()
	{
		var testee = watchlistItemFaker.Generate();
		var ratingKey = testee.RatingKey;
		await Assert.That(testee.SourceId).IsEqualTo(ratingKey);
	}
}
