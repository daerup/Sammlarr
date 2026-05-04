using API.TMDB;
using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Shared.Options;

namespace API.Tests.TMDB;

public class TmdbRatingServiceTests
{
	private const string TmdbBaseUrl = "https://api.themoviedb.org/3";

	[Test]
	public async Task GetRatingsAsync_WhenIdIsNotTmdbId_ReturnsEmpty()
	{
		var testee = new TmdbRatingService(null!);

		var result = await testee.GetRatingsAsync(ImdbId.From("tt100", MediaType.Movie));

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task GetRatingsAsync_WhenTmdbReturnsVoteAverage_ReturnsTmdbRatingWithVoteCount()
	{
		var faker = new Faker();
		var voteAverage = faker.Random.Double(1.0, 10.0);
		var voteCount = faker.Random.Int(1, 1_000_000);
		var json = $$"""
			{ "id": 42, "title": "X", "name": null, "media_type": "movie", "vote_average": {{voteAverage.ToString(
				System.Globalization.CultureInfo.InvariantCulture
			)}}, "vote_count": {{voteCount}} }
			""";
		var testee = CreateTestee(json);

		var result = await testee.GetRatingsAsync(TmdbId.From("42", MediaType.Movie));

		await Assert.That(result.Count).IsEqualTo(1);
		await Assert.That(result[0].Source).IsEqualTo(RatingSource.Tmdb);
		await Assert.That(result[0].NormalizedValue).IsEqualTo((float)voteAverage);
		await Assert.That(result[0].VoteCount).IsEqualTo(voteCount);
	}

	[Test]
	public async Task GetRatingsAsync_WhenVoteAverageIsZero_ReturnsEmpty()
	{
		var testee = CreateTestee("""{ "id": 42, "title": "X", "name": null, "media_type": "movie", "vote_average": 0, "vote_count": 0 }""");

		var result = await testee.GetRatingsAsync(TmdbId.From("42", MediaType.Movie));

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task GetRatingsAsync_WhenVoteAverageMissing_ReturnsEmpty()
	{
		var testee = CreateTestee("""{ "id": 42, "title": "X", "name": null, "media_type": "movie" }""");

		var result = await testee.GetRatingsAsync(TmdbId.From("42", MediaType.Movie));

		await Assert.That(result).IsEmpty();
	}

	private static TmdbRatingService CreateTestee(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		apiKeyStore.GetApiKeyAsync(MediaProvider.Tmdb, Any()).Returns("test-key");
		var options = new TmdbOptions { BaseUrl = TmdbBaseUrl, ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		var client = new TmdbClient(handler.CreateClient(TmdbBaseUrl), options, apiKeyStore);
		return new TmdbRatingService(new TmdbApi(client));
	}
}
