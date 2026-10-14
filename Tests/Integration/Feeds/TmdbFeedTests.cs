using Contracts.Auth;
using Contracts.Feed;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TmdbFeedTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string MixedList = """
		{"page":1,"total_pages":1,"items":[
		{"id":27205,"media_type":"movie","title":"Inception","release_date":"2010-07-15"},
		{"id":1396,"media_type":"tv","name":"Breaking Bad","first_air_date":"2008-01-20"}]}
		""";

	private IFeedProvider Testee => host.ResolveKeyed<IFeedProvider>(FeedProvider.Tmdb);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetFeedItemsAsync_ForAMixedTmdbList_LoadsTheMovieAndTheShow()
	{
		await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("27205"), Imdb("tt1375666")])).ServedByTmdb().SeedAsync();
		await host.Movie(TestMedia.BreakingBad).ServedByTmdb().SeedAsync();
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.TmdbApi}/list/42", MixedList);

		var items = await Testee.GetFeedItemsAsync("42", FieldNeeds.None).InListOrderAsync().ToListAsync();

		await Assert.That(items.Select(item => item.Details.Title)).IsEquivalentTo(["Inception", "Breaking Bad"]);
	}

	[Test]
	public async Task GetFeedItemsAsync_WithoutAKey_FailsNamingTheKey()
	{
		await Assert
			.That(async () => await Testee.GetFeedItemsAsync("42", FieldNeeds.None).InListOrderAsync().ToListAsync())
			.Throws<Exception>()
			.WithMessageContaining("No TMDB API key", StringComparison.Ordinal);
	}

	[Test]
	public async Task ClearAsync_OfTheReadToken_PausesNoTmdbFeed()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		var keys = host.Resolve<IApiKeyService>();
		await keys.StoreAsync(new TestApiKey(ApiKeyProvider.TmdbReadToken, "tmdb-read-token"));
		host.Context.Feeds.Add(
			new FeedRow
			{
				Name = "Best of 2026",
				IsEnabled = true,
				Provider = FeedProvider.Tmdb,
				Strategy = new Append(),
				Identifier = "42",
			}
		);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();

		await keys.ClearAsync(ApiKeyProvider.TmdbReadToken);

		await Assert.That((await host.Context.Feeds.AsNoTracking().SingleAsync()).IsEnabled).IsTrue();
	}
}
