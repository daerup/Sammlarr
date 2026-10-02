using API.Tmdb;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.TMDB;

internal static class TmdbApis
{
	internal static readonly TmdbOptions Options = new() { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p" };

	internal static TmdbApi Over(MockHttpHandler handler)
	{
		var apiKeys = IApiKeyService.Mock();
		apiKeys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "test-key")));
		return new TmdbApi(
			new TmdbClient(handler.CreateClient(Options.BaseUrl), Options, apiKeys.Object, ICurrentUser.Mock().Object),
			apiKeys.Object,
			NullLogger<TmdbApi>.Instance
		);
	}
}
