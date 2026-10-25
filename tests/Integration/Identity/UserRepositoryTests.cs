using Contracts.Identity;
using DB.Data.Auth;
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
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Identity;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UserRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	private const string Avatar = "https://plex.tv/users/first/avatar";

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IUserRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new UserRepository(fixture.ContextFactory, fixture.Resolve<IServiceScopeFactory>());

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
	public async Task ReplaceLoginAsync_PointsTheSameUserAtTheNewAccountAndForgetsTheOldOne()
	{
		var id = UserId.New();
		await _testee.CreateAsync(new User(id, "Lena", "avatar", UserRole.Admin, DateTimeOffset.UnixEpoch), AuthProvider.Plex, "lena-external");

		await _testee.ReplaceLoginAsync(id, AuthProvider.Plex, "daerup-external");

		await Assert.That((await _testee.FindByLoginAsync(AuthProvider.Plex, "daerup-external")).Map(user => user.Id)).IsSome().And.IsEqualTo(id);
		await Assert.That(await _testee.FindByLoginAsync(AuthProvider.Plex, "lena-external")).IsNone();
		await Assert.That(await _testee.FindRoleAsync(id)).IsSome().And.IsEqualTo(UserRole.Admin);
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
			IIdentityKey[] externalIds = [TmdbId.From("9100", MediaType.Movie), ImdbId.From("tt9100", MediaType.Movie)];
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
			seed.WantlistOutbox.Add(
				new WantlistOutboxRow
				{
					Media = media,
					Action = WantlistOperationAction.Add,
					Sink = SinkKind.Plex,
				}
			);
			seed.Blocklist.Add(new BlocklistRow { Media = media });
			seed.ApiKeySettings.Add(new ApiKeyConfigRow { Provider = ApiKeyProvider.Tmdb, ApiKey = new Secret("victim-key") });
			seed.Credentials.Add(
				new CredentialRow
				{
					Provider = AuthProvider.Plex,
					ClientId = Guid.NewGuid(),
					AccessToken = new Secret("token"),
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
	public async Task CreateAsync_WhenItsRoleCannotBeGiven_LeavesNoUserBehind()
	{
		await using (var ctx = await fixture.ContextFactory.CreateDbContextAsync())
		{
			await ctx.Roles.Where(role => role.Name == nameof(UserRole.Member)).ExecuteDeleteAsync();
		}

		var stranger = new User(UserId.New(), "Stranger", Avatar, UserRole.Member, DateTimeOffset.UnixEpoch);

		await Assert.That(() => _testee.CreateAsync(stranger, AuthProvider.Plex, "stranger-external")).Throws<InvalidOperationException>();

		await using var check = await fixture.ContextFactory.CreateDbContextAsync();
		await Assert.That(await check.Users.AnyAsync(user => user.Id == stranger.Id.Value)).IsFalse();
	}

	[Test]
	public async Task ChangeRoleAsync_WhenTheNewRoleCannotBeGiven_KeepsTheOldOne()
	{
		var person = await CreateAsync("plex-member", DateTimeOffset.UnixEpoch);
		await using (var ctx = await fixture.ContextFactory.CreateDbContextAsync())
		{
			await ctx.Roles.Where(role => role.Name == nameof(UserRole.Admin)).ExecuteDeleteAsync();
		}

		await Assert.That(() => _testee.ChangeRoleAsync(person, UserRole.Admin)).Throws<InvalidOperationException>();

		await Assert.That(await _testee.FindRoleAsync(person)).IsSome().And.IsEqualTo(UserRole.Member);
	}

	[Test]
	public async Task CreateAsync_LeavesTheColourForTheUsersListToPaint()
	{
		var person = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));

		var stored = (await _testee.GetColoursAsync())[person];
		await Assert.That(stored.GoldenHue).IsNone();
		await Assert.That(stored.Picture).IsNone();
	}

	[Test]
	public async Task StoreColourAsync_ForAColourfulPicture_GivesBackTheGoldenHueAndThePicturesTone()
	{
		var person = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));

		await _testee.StoreColourAsync(person, new StoredColour(Some.Of(177.5), Some.Of(new StoredPicture(Avatar, Some.Of(new PictureTone(195, 0.09))))));

		var stored = (await _testee.GetColoursAsync())[person];
		await Assert.That(stored.GoldenHue).IsSome().And.IsEqualTo(177.5);
		await Assert.That(stored.Picture.Map(picture => picture.Url)).IsSome().And.IsEqualTo(Avatar);
		await Assert.That(stored.Picture.Map(picture => picture.Tone)).IsSome().And.IsEqualTo(new PictureTone(195, 0.09));
	}

	[Test]
	public async Task StoreColourAsync_ForSomeoneAlreadyRemoved_ChangesNothing()
	{
		var person = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));
		await _testee.DeleteAsync(person);

		await _testee.StoreColourAsync(person, new StoredColour(Some.Of(40d), new None()));

		await Assert.That((await _testee.GetColoursAsync()).ContainsKey(person)).IsFalse();
	}

	[Test]
	public async Task StoreColourAsync_ForAGreyPicture_RemembersThePictureWithoutATone()
	{
		var person = await CreateAsync("plex-first", DateTimeOffset.UnixEpoch.AddYears(60));

		await _testee.StoreColourAsync(person, new StoredColour(Some.Of(40d), Some.Of(new StoredPicture(Avatar, new None()))));

		var stored = (await _testee.GetColoursAsync())[person];
		await Assert.That(stored.Picture.Map(picture => picture.Url)).IsSome().And.IsEqualTo(Avatar);
		await Assert.That(stored.Picture.Map(picture => picture.Tone)).IsNone();
	}

	private async Task<UserId> CreateAsync(string externalUserId, DateTimeOffset joinedAt)
	{
		var user = new User(UserId.New(), externalUserId, string.Empty, UserRole.Member, joinedAt);
		await _testee.CreateAsync(user, AuthProvider.Plex, externalUserId);
		return user.Id;
	}
}
