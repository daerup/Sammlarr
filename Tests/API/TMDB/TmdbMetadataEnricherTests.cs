using API.Tmdb;
using Contracts.Auth;
using Contracts.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.TMDB;

public class TmdbMetadataEnricherTests
{
	[Test]
	public async Task LookUpAsync_MapsTmdbRatingFromDetail()
	{
		var options = new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":123,\"title\":\"Example\",\"vote_average\":7.5,\"vote_count\":1000}");
		var apiKeyStore = IApiKeyConfigRepository.Mock();
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Tmdb, "test-key");
		apiKeyStore.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of(apiKey));
		var tmdbApi = new TmdbApi(new TmdbClient(handler.CreateClient(options.BaseUrl), options), apiKeyStore.Object, NullLogger<TmdbApi>.Instance);
		var testee = new TmdbMetadataEnricher(tmdbApi, options);

		var result = await testee.LookUpAsync(TmdbId.From("123", MediaType.Movie));

		await Assert.That(result).IsNotNull();
		var identified = result!.Value.AsIdentified();
		await Assert.That(identified).IsNotNull();
		var rating = identified!.Value.Rating;
		using var _ = Assert.Multiple();
		await Assert.That(rating.Sources.Any(source => source.Source == RatingProvider.Tmdb)).IsTrue();
		await Assert.That(rating.Average).IsEqualTo(7.5f);
	}
}
