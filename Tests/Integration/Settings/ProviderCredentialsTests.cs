using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Watchlist;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ProviderCredentialsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IApiKeyConfigRepository ApiKeys => host.Resolve<IApiKeyConfigRepository>();
	private IApiKeyFactory ApiKeyFactory => host.Resolve<IApiKeyFactory>();
	private IMediaRatingService TmdbRatings => host.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb);
	private IWatchlistProvider PlexWatchlist => host.ResolveKeyed<IWatchlistProvider>(WatchlistProvider.Plex);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Looking_up_a_key_that_was_never_stored_returns_nothing() => await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();

	[Test]
	public async Task A_stored_key_can_be_retrieved()
	{
		await ApiKeys.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key-12345"));

		await Assert
			.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("tmdb-key-12345"));
	}

	[Test]
	public async Task Storing_a_key_replaces_the_previous_value()
	{
		await ApiKeys.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "old-key"));

		await ApiKeys.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "new-key"));

		await Assert
			.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("new-key"));
	}

	[Test]
	public async Task Clearing_a_key_removes_it()
	{
		await ApiKeys.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "secret-key"));

		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task Clearing_a_key_that_was_never_stored_is_safe()
	{
		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task Tmdb_rejects_a_key_it_does_not_recognise()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "wrong-key")).IsError();
	}

	[Test]
	public async Task Tmdb_accepts_the_configured_key()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "valid-tmdb-key")).IsSuccess();
	}

	[Test]
	public async Task Mdblist_rejects_a_key_it_does_not_recognise()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "wrong-key")).IsError();
	}

	[Test]
	public async Task Mdblist_accepts_the_configured_key()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "valid-mdblist-key")).IsSuccess();
	}

	[Test]
	public async Task Verifying_a_key_fails_gracefully_when_the_provider_is_unreachable()
	{
		host.TmdbUnreachable();

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "any-key")).IsError();
	}

	[Test]
	public async Task Tmdb_ratings_are_unavailable_without_a_key()
	{
		var ratings = (await TmdbRatings.GetRatingsAsync(TmdbId.From("550", MediaType.Movie))).GetValueOr([]);

		await Assert.That(ratings).IsEmpty();
	}

	[Test]
	public async Task Tmdb_ratings_load_once_the_key_is_configured()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		host.Movie("Test Movie", tmdb: "550").ServedByTmdb(rating: 7.8, votes: 5000);

		var ratings = (await TmdbRatings.GetRatingsAsync(TmdbId.From("550", MediaType.Movie))).GetValueOr([]);

		await Assert.That(ratings).IsNotEmpty();
	}

	[Test]
	public async Task Plex_watchlist_pushes_fail_without_a_token()
	{
		var results = await PlexWatchlist.AddToWatchlistAsync([PlexId.From("plex-100", MediaType.Movie)]).ToListAsync();

		await Assert.That(results[^1].Success).IsFalse();
	}

	[Test]
	public async Task Plex_watchlist_pushes_succeed_once_the_token_is_configured()
	{
		await host.WithPlexAccount("plex-access-token");

		var results = await PlexWatchlist.AddToWatchlistAsync([PlexId.From("plex-100", MediaType.Movie)]).ToListAsync();

		await Assert.That(results[^1].Success).IsTrue();
	}

	[Test]
	public async Task Clearing_the_tmdb_key_removes_its_cached_ratings()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();

		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		host.ScopedDbContext.ChangeTracker.Clear();
		var cached = await host.ScopedDbContext.MediaRatings.AsNoTracking().AnyAsync(rating => rating.Source == RatingProvider.Tmdb);
		await Assert.That(cached).IsFalse();
	}
}
