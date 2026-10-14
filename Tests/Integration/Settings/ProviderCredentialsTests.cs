using System.Net;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts.Auth;
using Contracts.Media;
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
	private IDetailsEnricher Tmdb => host.Enricher(RemoteService.Tmdb);
	private IMediaSink PlexSink => host.ResolveKeyed<IMediaSink>(SinkKind.Plex);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetAsync_WhenNoKeyWasStored_ReturnsNothing() => await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();

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
	public async Task TestAsync_WhenTmdbRejectsTheKey_ReportsItInvalid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeys.TestAsync(ApiKeyProvider.Tmdb, "wrong-key")).IsError();
	}

	[Test]
	public async Task TestAsync_WhenTmdbAcceptsTheKey_ReportsItValid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.TmdbApi}/{TmdbEndpoints.Configuration}", "{}");

		await Assert.That(await ApiKeys.TestAsync(ApiKeyProvider.Tmdb, "valid-tmdb-key")).IsSuccess();
	}

	[Test]
	public async Task TestAsync_WhenMdblistRejectsTheKey_ReportsItInvalid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeys.TestAsync(ApiKeyProvider.Mdblist, "wrong-key")).IsError();
	}

	[Test]
	public async Task TestAsync_WhenMdblistAcceptsTheKey_ReportsItValid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/{MdblistEndpoints.User}", "{}");

		await Assert.That(await ApiKeys.TestAsync(ApiKeyProvider.Mdblist, "valid-mdblist-key")).IsSuccess();
	}

	[Test]
	public async Task TestAsync_WhenTheProviderIsUnreachable_FailsGracefully()
	{
		host.TmdbUnreachable();

		await Assert.That(await ApiKeys.TestAsync(ApiKeyProvider.Tmdb, "any-key")).IsError();
	}

	[Test]
	public async Task GetAsync_AfterTmdbRefusesTheStoredKey_FailsUntilAKeyIsStoredAgain()
	{
		await host.WithTmdbAccount("valid-tmdb-key");
		host.Movie("Test Movie", tmdb: "550").ServedByTmdb(rating: 7.8, votes: 5000);
		host.HttpStub.RequireQueryParam(TestUrls.TmdbApi, "api_key", "rotated-tmdb-key");

		await Tmdb.LookUpAsync(TmdbId.From("550", MediaType.Movie), []);

		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsError();
		await ApiKeys.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "rotated-tmdb-key"));
		await Assert.That(await ApiKeys.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess();
	}

	[Test]
	public async Task LookUpAsync_WhenTheTmdbKeyIsMissing_IsNoMatch()
	{
		await Assert.That(await Tmdb.LookUpAsync(TmdbId.From("550", MediaType.Movie), [])).IsOfUnionType<Answer<FoundMedia>, NoMatch>();
	}

	[Test]
	public async Task ApplyAsync_WhenThePlexTokenIsMissing_Fails()
	{
		var solaris = await host.Movie("Solaris", plex: "plex-100").SeedAsync();

		var acks = await PlexSink.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(solaris, new None())]).ToListAsync();

		await Assert.That(acks.Single().Outcome).IsEqualTo(SinkSyncOutcome.Failed);
	}

	[Test]
	public async Task ApplyAsync_WhenThePlexTokenIsConfigured_Succeeds()
	{
		await host.WithPlexAccount("plex-access-token");
		host.HttpStub.RespondWithStatusForUrl(PlexEndpoints.AddToWatchlistPath, HttpStatusCode.OK);
		var solaris = await host.Movie("Solaris", plex: "plex-100").SeedAsync();

		var acks = await PlexSink.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(solaris, new None())]).ToListAsync();

		await Assert.That(acks.Single().Outcome).IsEqualTo(SinkSyncOutcome.Applied);
	}

	[Test]
	public async Task ClearAsync_WhenTheTmdbKeyIsCleared_LeavesTheCachedRatingsToTheCachePurger()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Resolve<IMediaService>().GetDetailsAsync([fightClub], FieldNeeds.Everything).ToListAsync();

		await ApiKeys.ClearAsync(ApiKeyProvider.Tmdb);

		host.ScopedDbContext.ChangeTracker.Clear();
		var cached = await host.ScopedDbContext.MediaRatings.AsNoTracking().AnyAsync(rating => rating.Source == RatingSource.Tmdb);
		await Assert.That(cached).IsTrue();
	}
}
