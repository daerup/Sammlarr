using Contracts.Identity;
using DB.Data.Access;
using DB.Data.Blacklist;
using DB.Data.Feed;
using DB.Data.Identity;
using DB.Data.Media;
using DB.Data.Watchlist;
using DB.Data.Watchlist.Outbox;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IUserRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new UserRepository(fixture.ContextFactory);

	[Test]
	public async Task GetAsync_ReturnsStoredUserWithRealRole()
	{
		var user = await _testee.GetAsync(fixture.CurrentUser.Id);

		await Assert.That(user.Id).IsEqualTo(fixture.CurrentUser.Id);
		await Assert.That(user.Role).IsEqualTo(fixture.CurrentUser.Role);
		await Assert.That(user.DisplayName).IsEqualTo(fixture.CurrentUser.DisplayName);
	}

	[Test]
	public async Task GetAllAsync_ProjectsUsersWithJoinedAt()
	{
		var joinedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
		var bobId = UserId.New();
		await _testee.CreateAsync(new User(bobId, "Bob", "avatar", UserRole.Member, joinedAt), AuthProvider.Plex, "bob-external");

		var all = await _testee.GetAllAsync();

		var bob = all.Single(user => user.Id == bobId);
		await Assert.That(all.Count).IsEqualTo(2);
		await Assert.That(bob.DisplayName).IsEqualTo("Bob");
		await Assert.That(bob.JoinedAt).IsEqualTo(joinedAt);
	}

	[Test]
	public async Task DeleteAsync_RemovesUserAndCascadesAllOwnedData()
	{
		var victimId = UserId.New();
		await _testee.CreateAsync(new User(victimId, "Victim", "avatar", UserRole.Member, DateTimeOffset.UnixEpoch), AuthProvider.Plex, "victim-external");

		var mediaId = await SeedOwnedDataForAsync(victimId);

		await AssertOwnedRowCountAsync(victimId, expected: 1);

		await _testee.DeleteAsync(victimId);

		await AssertOwnedRowCountAsync(victimId, expected: 0);
		await Assert.That((await _testee.GetAllAsync()).Any(user => user.Id == victimId)).IsFalse();

		await using var ctx = fixture.ContextFactory.CreateDbContext();
		await Assert.That(await ctx.Media.AnyAsync(media => media.Id == mediaId)).IsTrue();
	}

	private async Task<Guid> SeedOwnedDataForAsync(UserId ownerId)
	{
		var previous = fixture.CurrentUser.Id;
		fixture.CurrentUser.Id = ownerId;

		Guid mediaId;
		await using (var seed = fixture.ContextFactory.CreateDbContext())
		{
			var media = new MediaRow();
			IExternalMediaId[] externalIds = [TmdbId.From("9100", MediaType.Movie), ImdbId.From("tt9100", MediaType.Movie)];
			await seed.ExternalMediaIds.AddRangeAsync(externalIds.Select(id => id.ToExternalIdEntity(media)));

			seed.Feeds.Add(
				new FeedRow
				{
					Name = "Victim Feed",
					IsEnabled = true,
					Provider = FeedProvider.Mdblist,
					Strategy = FeedAddStrategy.Append,
					Identifier = "victim-feed",
				}
			);
			seed.Watchlist.Add(new WatchlistRow { Media = media, Origin = WatchlistEntryOrigin.Manual });
			seed.WatchlistOutbox.Add(new WatchlistOutboxRow { Media = media, Action = WatchlistOperationAction.Add });
			seed.Blacklist.Add(new BlacklistRow { Media = media });
			seed.ApiKeySettings.Add(new ApiKeyConfigRow { Provider = ApiKeyProvider.Tmdb, ApiKey = "victim-key" });
			seed.Credentials.Add(
				new CredentialRow
				{
					Provider = AuthProvider.Plex,
					ClientId = Guid.NewGuid(),
					AccessToken = "token",
					ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
				}
			);
			await seed.SaveChangesAsync();
			mediaId = media.Id;
		}

		fixture.CurrentUser.Id = previous;
		return mediaId;
	}

	private async Task AssertOwnedRowCountAsync(UserId userId, int expected)
	{
		await using var ctx = fixture.ContextFactory.CreateDbContext();
		await Assert.That(await CountForAsync(ctx.Feeds, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.Watchlist, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.WatchlistOutbox, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.Blacklist, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.ApiKeySettings, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.Credentials, userId)).IsEqualTo(expected);
	}

	private static Task<int> CountForAsync<TRow>(DbSet<TRow> set, UserId userId)
		where TRow : class => set.IgnoreQueryFilters().CountAsync(row => EF.Property<Guid>(row, "UserId") == userId.Value);
}
