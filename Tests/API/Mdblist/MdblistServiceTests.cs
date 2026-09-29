using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using API.Mdblist;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Domain.Auth;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Mdblist;

public class MdblistServiceTests
{
	private const string BaseUrl = "https://api.mdblist.com";

	[Test]
	public async Task GetFeedItemsAsync_WhenIdentifierReturns404_ThrowsFeedIdentifierNotFound()
	{
		const string identifier = "missing-list";
		var testee = CreateTestee(new StubHandler(HttpStatusCode.NotFound), out _);

		var act = async () =>
		{
			await foreach (var _ in testee.GetFeedItemsAsync(identifier)) { }
		};

		var exception = await Assert.That(act).Throws<FeedIdentifierNotFoundException>();
		await Assert.That(exception!.Identifier).IsEqualTo(identifier);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheListSpansPages_ResolvesEveryPageInOneStream()
	{
		var testee = CreateTestee(new StubHandler(HttpStatusCode.OK, pages: 2), out var media);

		var results = await testee.GetFeedItemsAsync("multi-page").ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		media.ResolveDetailsAsync(Any<IAsyncEnumerable<ResolveRequest>>(), Any()).WasCalled(Times.Once);
	}

	private static MdblistService CreateTestee(HttpMessageHandler handler, out Mock<IMediaService> media)
	{
		var apiKeyStore = IApiKeyService.Mock();
		IApiKey apiKey = new TestApiKey(ApiKeyProvider.Mdblist, "test-key");
		apiKeyStore.GetApiKeyAsync(ApiKeyProvider.Mdblist, Any()).Returns(Success.Of(apiKey));
		var options = new MdblistOptions { BaseUrl = BaseUrl };
		var client = new MdblistClient(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, options, apiKeyStore.Object, ICurrentUser.Mock().Object);
		media = IMediaService.Mock();
		media.ResolveDetailsAsync(Any<IAsyncEnumerable<ResolveRequest>>(), Any()).Returns((requests, token) => Realize(requests, token));
		var providerConfig = IProviderConfigService.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		return new MdblistService(client, media.Object, providerConfig.Object, apiKeyStore.Object);
	}

	private sealed class StubHandler(HttpStatusCode status, int pages = 1) : HttpMessageHandler
	{
		private int _calls;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var index = Interlocked.Increment(ref _calls);
			var response = new HttpResponseMessage(status);
			if (status is not HttpStatusCode.OK)
			{
				return Task.FromResult(response);
			}

			response.Content = new StringContent(Page($"page-{index}"), Encoding.UTF8, "application/json");
			if (index < pages)
			{
				response.Headers.Add("X-Has-More", "true");
			}
			return Task.FromResult(response);
		}

		private static string Page(string title) =>
			$$"""{"movies":[{"id":1,"ids":{"imdb":null,"tmdb":{{title.GetHashCode(StringComparison.Ordinal) & 0xffff}},"tvdb":null,"mdblist":null,"mal":null},"rank":1,"title":"{{title}}","release_year":2020}],"shows":[]}""";
	}

	private static async IAsyncEnumerable<MediaLookup> Realize(
		IAsyncEnumerable<ResolveRequest> requests,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		await foreach (var request in requests.WithCancellation(cancellationToken))
		{
			yield return new UnmatchedMedia { Externals = [.. request.Candidates] };
		}
	}
}
