using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
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
	private IMediaSink PlexWantlist => host.ResolveKeyed<IMediaSink>(SinkKind.Plex);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetApiKeyAsync_WhenNoKeyWasStored_ReturnsNothing() => await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();

	[Test]
	public async Task GetApiKeyAsync_WhenAKeyWasStored_ReturnsIt()
	{
		await ApiKeys.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key-12345"));

		await Assert
			.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("tmdb-key-12345"));
	}

	[Test]
	public async Task SetApiKeyAsync_WhenAKeyAlreadyExists_ReplacesIt()
	{
		await ApiKeys.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "old-key"));

		await ApiKeys.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "new-key"));

		await Assert
			.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("new-key"));
	}

	[Test]
	public async Task ClearApiKeyAsync_WhenAKeyExists_RemovesIt()
	{
		await ApiKeys.SaveApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, "secret-key"));

		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task ClearApiKeyAsync_WhenNoKeyWasStored_IsSafe()
	{
		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await ApiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsError();
	}

	[Test]
	public async Task VerifyAsync_WhenTmdbRejectsTheKey_ReportsItInvalid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "wrong-key")).IsError();
	}

	[Test]
	public async Task VerifyAsync_WhenTmdbAcceptsTheKey_ReportsItValid()
	{
		await host.WithTmdbAccount("valid-tmdb-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Tmdb, "valid-tmdb-key")).IsSuccess();
	}

	[Test]
	public async Task VerifyAsync_WhenMdblistRejectsTheKey_ReportsItInvalid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "wrong-key")).IsError();
	}

	[Test]
	public async Task VerifyAsync_WhenMdblistAcceptsTheKey_ReportsItValid()
	{
		await host.WithMdblistAccount("valid-mdblist-key");

		await Assert.That(await ApiKeyFactory.CreateAsync(ApiKeyProvider.Mdblist, "valid-mdblist-key")).IsSuccess();
	}

	[Test]
	public async Task VerifyAsync_WhenTheProviderIsUnreachable_FailsGracefully()
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
	public async Task PushAsync_WhenThePlexTokenIsMissing_Fails()
	{
		var results = await PlexWantlist.AddToWantlistAsync([PlexId.From("plex-100", MediaType.Movie)]).ToListAsync();

		await Assert.That(results[^1].Success).IsFalse();
	}

	[Test]
	public async Task PushAsync_WhenThePlexTokenIsConfigured_Succeeds()
	{
		await host.WithPlexAccount("plex-access-token");

		var results = await PlexWantlist.AddToWantlistAsync([PlexId.From("plex-100", MediaType.Movie)]).ToListAsync();

		await Assert.That(results[^1].Success).IsTrue();
	}

	[Test]
	public async Task ClearApiKeyAsync_WhenTheTmdbKeyIsCleared_LeavesTheCachedRatingsToTheCachePurger()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();

		await ApiKeys.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		host.ScopedDbContext.ChangeTracker.Clear();
		var cached = await host.ScopedDbContext.MediaRatings.AsNoTracking().AnyAsync(rating => rating.Source == RatingProvider.Tmdb);
		await Assert.That(cached).IsTrue();
	}
}
