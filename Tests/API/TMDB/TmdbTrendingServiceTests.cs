using API.Tmdb;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

public class TmdbTrendingServiceTests
{
	[Test]
	public async Task GetTrendingAsync_WhenAResultHasNoTitleAndNoPoster_ReportsItUnmatchedWithThoseUnknown()
	{
		var handler = Mock.HttpHandler();
		handler
			.OnAnyRequest()
			.RespondWithJson(
				"""{"page":1,"results":[{"adult":false,"backdrop_path":"","id":7,"title":null,"name":null,"overview":" ","poster_path":null,"media_type":"movie","original_language":"en","genre_ids":[],"popularity":1.0,"release_date":"2024-05-01","video":false,"vote_average":6.5,"vote_count":120}],"total_pages":1,"total_results":1}"""
			);
		var media = IMediaService.Mock();
		media
			.IdentifyAsync(Any<IEnumerable<IdentifyRequest>>(), Any())
			.Returns((requests, _) => requests.Select(_ => (Result<MediaIdentity>)Error.From("not found")).ToAsyncEnumerable());

		var page = await Testee(handler, media.Object).GetTrendingAsync(1);

		await Assert
			.That(page.Items.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched =>
				await Assert
					.That(unmatched.Details)
					.IsEqualTo(
						new MovieDetails { ReleaseDate = new DateOnly(2024, 5, 1), Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 6.5f, Some.Of(120))]) }
					)
			);
		media.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests != null && requests.Single().Hints.IsNone()), Any()).WasCalled(Times.Once);
	}

	private static TmdbTrendingService Testee(MockHttpHandler handler, IMediaService media)
	{
		var apiKeys = IApiKeyService.Mock();
		apiKeys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "test-key")));
		var client = new TmdbClient(handler.CreateClient(TmdbApis.Options.BaseUrl), TmdbApis.Options, apiKeys.Object, ICurrentUser.Mock().Object);
		return new TmdbTrendingService(client, new TmdbArtwork(TmdbApis.Options), media, apiKeys.Object);
	}
}
