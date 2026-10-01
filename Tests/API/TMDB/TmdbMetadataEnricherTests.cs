using System.Net;
using API.Tmdb;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

public class TmdbMetadataEnricherTests
{
	private static readonly TmdbOptions Options = new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };
	private static readonly TmdbId Id = TmdbId.From("123", MediaType.Movie);

	[Test]
	public async Task LookUpAsync_MapsTmdbRatingFromDetail()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson("{\"id\":123,\"title\":\"Example\",\"vote_average\":7.5,\"vote_count\":1000}");

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async lookup =>
			{
				var identified = lookup.AsIdentified();
				await Assert.That(identified).IsNotNull();
				var rating = identified!.Value.Rating;
				await Assert.That(rating.Sources.Any(source => source.Source == RatingProvider.Tmdb)).IsTrue();
				await Assert.That(rating.Average).IsEqualTo(7.5f);
			});
	}

	[Test]
	[Arguments(HttpStatusCode.TooManyRequests)]
	[Arguments(HttpStatusCode.InternalServerError)]
	public async Task LookUpAsync_WhenTmdbIsUnavailable_FailsAsUnavailable(HttpStatusCode status)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(status);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsTypeOf<ProviderUnavailableException>());
	}

	[Test]
	public async Task LookUpAsync_WhenTmdbDoesNotKnowTheTitle_FailsAsAnAnswer()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);

		var result = await CreateTestee(handler).LookUpAsync(Id);

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Exception).IsNull());
	}

	private static TmdbMetadataEnricher CreateTestee(MockHttpHandler handler)
	{
		var apiKeyStore = IApiKeyService.Mock();
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Tmdb, "test-key");
		apiKeyStore.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of(apiKey));
		var tmdbApi = new TmdbApi(
			new TmdbClient(handler.CreateClient(Options.BaseUrl), Options, apiKeyStore.Object, ICurrentUser.Mock().Object),
			apiKeyStore.Object,
			NullLogger<TmdbApi>.Instance
		);
		return new TmdbMetadataEnricher(tmdbApi, new TmdbArtwork(Options));
	}
}
