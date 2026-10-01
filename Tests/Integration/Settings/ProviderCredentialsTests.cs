using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ProviderCredentialsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IApiKeyService ApiKeys => host.Resolve<IApiKeyService>();
	private IApiKeyFactory ApiKeyFactory => host.Resolve<IApiKeyFactory>();
	private IMediaRatingService TmdbRatings => host.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb);
	private IMediaSink PlexSink => host.ResolveKeyed<IMediaSink>(SinkKind.Plex);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetAsync_WhenNoKeyWasStored_ReturnsNothing() => await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();

	[Test]
	public async Task GetAsync_WhenAKeyWasStored_ReturnsIt()
	{
		await ApiKeys.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key-12345"));

		await Assert
			.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("tmdb-key-12345"));
	}

	[Test]
	public async Task StoreAsync_WhenAKeyAlreadyExists_ReplacesIt()
	{
		await ApiKeys.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "old-key"));

		await ApiKeys.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "new-key"));

		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess().And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("new-key"));
	}

	[Test]
	public async Task ClearAsync_WhenAKeyExists_RemovesIt()
	{
		await ApiKeys.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "secret-key"));

		await ApiKeys.ClearAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task ClearAsync_WhenNoKeyWasStored_IsSafe()
	{
		await ApiKeys.ClearAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task CreateAsync_WhenTmdbRejectsTheKey_ReportsItInvalid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "wrong-key")).IsError();
	}

	[Test]
	public async Task CreateAsync_WhenTmdbAcceptsTheKey_ReportsItValid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "valid-tmdb-key")).IsSuccess();
	}

	[Test]
	public async Task CreateAsync_WhenMdblistRejectsTheKey_ReportsItInvalid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "wrong-key")).IsError();
	}

	[Test]
	public async Task CreateAsync_WhenMdblistAcceptsTheKey_ReportsItValid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "valid-mdblist-key")).IsSuccess();
	}

	[Test]
	public async Task CreateAsync_WhenTheProviderIsUnreachable_FailsGracefully()
	{
		host.TmdbUnreachable();

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "any-key")).IsError();
	}

	[Test]
	public async Task GetRatingsAsync_WhenTheTmdbKeyIsMissing_AreUnavailable()
	{
		var ratings = (await TmdbRatings.GetRatingsAsync(TmdbId.From("550", MediaType.Movie))).GetValueOr([]);

		await Assert.That(ratings).IsEmpty();
	}

	[Test]
	public async Task GetRatingsAsync_WhenTheTmdbKeyIsConfigured_LoadsThem()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		host.Movie("Test Movie", tmdb: "550").ServedByTmdb(rating: 7.8, votes: 5000);

		var ratings = (await TmdbRatings.GetRatingsAsync(TmdbId.From("550", MediaType.Movie))).GetValueOr([]);

		await Assert.That(ratings).IsNotEmpty();
	}

	[Test]
	public async Task ApplyAsync_WhenThePlexTokenIsMissing_Fails()
	{
		var solaris = await host.Movie("Solaris", plex: "plex-100").SeedAsync();

		var acks = await PlexSink.ApplyAsync(WantlistOperationAction.Add, [solaris]).ToListAsync();

		await Assert.That(acks.Single().Outcome).IsEqualTo(SinkSyncOutcome.Failed);
	}

	[Test]
	public async Task ApplyAsync_WhenThePlexTokenIsConfigured_Succeeds()
	{
		await host.WithPlexAccount("plex-access-token");
		var solaris = await host.Movie("Solaris", plex: "plex-100").SeedAsync();

		var acks = await PlexSink.ApplyAsync(WantlistOperationAction.Add, [solaris]).ToListAsync();

		await Assert.That(acks.Single().Outcome).IsEqualTo(SinkSyncOutcome.Applied);
	}

	[Test]
	public async Task ClearAsync_WhenTheTmdbKeyIsCleared_LeavesTheCachedRatingsToTheCachePurger()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();

		await ApiKeys.ClearAsync(ApiKeyProvider.Tmdb);

		host.ScopedDbContext.ChangeTracker.Clear();
		var cached = await host.ScopedDbContext.MediaRatings.AsNoTracking().AnyAsync(rating => rating.Source == RatingProvider.Tmdb);
		await Assert.That(cached).IsTrue();
	}
}
