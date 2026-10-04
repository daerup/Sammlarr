using Contracts.Identity;
using DB.Data.Access;
using DB.Data.Blocklist;
using DB.Data.Feed;
using DB.Data.Identity;
using DB.Data.Media;
using DB.Data.Wantlist;
using DB.Data.Wantlist.Outbox;
using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
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
					Strategy = new FeedStrategy(new Append()),
					Identifier = "victim-feed",
				}
			);
			seed.Wantlist.Add(new WantlistRow { Media = media, Origin = WantlistItemOrigin.Manual });
			seed.WantlistOutbox.Add(new WantlistOutboxRow { Media = media, Action = WantlistOperationAction.Add });
			seed.Blocklist.Add(new BlocklistRow { Media = media });
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
		await Assert.That(await CountForAsync(ctx.Wantlist, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.WantlistOutbox, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.Blocklist, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.ApiKeySettings, userId)).IsEqualTo(expected);
		await Assert.That(await CountForAsync(ctx.Credentials, userId)).IsEqualTo(expected);
	}

	private static Task<int> CountForAsync<TRow>(DbSet<TRow> set, UserId userId)
		where TRow : class => set.IgnoreQueryFilters().CountAsync(row => EF.Property<Guid>(row, "UserId") == userId.Value);

	[Test]
	public async Task CreateAsync_ForEachNewPerson_TakesTheNextColourFromTheLatestOne()
	{
		var latest = (await _testee.GetHuesAsync())[fixture.CurrentUser.Id];

		var first = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));
		var second = await CreateAsync("plex-second", DateTimeOffset.UnixEpoch.AddYears(61));

		var hues = await _testee.GetHuesAsync();
		await Assert.That(hues[first]).IsEqualTo(PersonHues.After(Some.Of(latest)));
		await Assert.That(hues[second]).IsEqualTo(PersonHues.After(Some.Of(hues[first])));
	}

	[Test]
	public async Task DeleteAsync_OfSomeone_ChangesNobodyElsesColour()
	{
		var first = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));
		var second = await CreateAsync("plex-second", DateTimeOffset.UnixEpoch.AddYears(61));
		var before = await _testee.GetHuesAsync();

		await _testee.DeleteAsync(first);

		await Assert.That((await _testee.GetHuesAsync())[second]).IsEqualTo(before[second]);
	}

	[Test]
	public async Task CreateAsync_ForPeopleWhoJoinedInTheSameInstant_TakesTheColourAfterTheLastById()
	{
		var at = DateTimeOffset.UnixEpoch.AddYears(60);
		var one = await CreateAsync("plex-one", at);
		var two = await CreateAsync("plex-two", at);
		var hues = await _testee.GetHuesAsync();
		var last = string.CompareOrdinal(Stored(one), Stored(two)) > 0 ? one : two;

		var third = await CreateAsync("plex-three", at.AddYears(1));

		await Assert.That((await _testee.GetHuesAsync())[third]).IsEqualTo(PersonHues.After(Some.Of(hues[last])));
	}

	private static string Stored(UserId id) => id.Value.ToString().ToUpperInvariant();

	private async Task<UserId> CreateAsync(string externalUserId, DateTimeOffset joinedAt)
	{
		var user = new User(UserId.New(), externalUserId, string.Empty, UserRole.Member, joinedAt);
		await _testee.CreateAsync(user, AuthProvider.Plex, externalUserId);
		return user.Id;
	}
}
