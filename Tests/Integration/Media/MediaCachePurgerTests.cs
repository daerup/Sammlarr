using Contracts;
using Contracts.Media.Metadata;
using Contracts.Settings;
using DB.Data.Blocklist;
using DB.Data.Cleanup;
using DB.Data.Identity;
using DB.Data.Media;
using DB.Data.Media.Metadata;
using DB.Data.Wantlist;
using DB.Data.Wantlist.Outbox;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaCachePurgerTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IMediaCachePurger _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		var configRepository = IProviderConfigRepository.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		_testee = new MediaCachePurger(fixture.ContextFactory, configRepository.Object, fixture.CurrentUser, NullLogger<MediaCachePurger>.Instance);
	}

	[Test]
	public async Task PurgeAsync_WhenNotAdmin_Throws()
	{
		var configRepository = IProviderConfigRepository.Mock();
		var member = new TestCurrentUser { Role = UserRole.Member };
		var testee = new MediaCachePurger(fixture.ContextFactory, configRepository.Object, member, NullLogger<MediaCachePurger>.Instance);

		await Assert.That(async () => await testee.PurgeAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsBlocklisted_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var blocklistedId = PlexId.From("2000", MediaType.Movie);
		var blocklistedImdb = ImdbId.From("tt2000", MediaType.Movie);
		var purgedId = TmdbId.From("2001", MediaType.Movie);

		var blocklistedMedia = new MediaRow();
		var purgedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(
			blocklistedId.ToExternalIdEntity(blocklistedMedia),
			blocklistedImdb.ToExternalIdEntity(blocklistedMedia),
			purgedId.ToExternalIdEntity(purgedMedia)
		);
		var blocklistedMetadata = BuildMetadata(blocklistedMedia, "Blocklisted");
		var purgedMetadata = BuildMetadata(purgedMedia, "Purged");
		fixture.Context.MediaMetadata.AddRange(blocklistedMetadata, purgedMetadata);
		fixture.Context.MediaRatings.AddRange(
			new MediaRatingRow
			{
				Source = RatingProvider.Tmdb,
				NormalizedValue = 8.0f,
				VoteCount = 1234,
				Metadata = blocklistedMetadata,
			},
			new MediaRatingRow
			{
				Source = RatingProvider.Tmdb,
				NormalizedValue = 7.0f,
				VoteCount = 567,
				Metadata = purgedMetadata,
			}
		);
		fixture.Context.Blocklist.Add(new BlocklistRow { Media = blocklistedMedia });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.MediaMetadata.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.MediaRatings.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.Blocklist.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(blocklistedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds).IsSingleElement();
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(blocklistedId);
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsWantlisted_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var wantlistedId = PlexId.From("3000", MediaType.Movie);
		var wantlistedImdb = ImdbId.From("tt3000", MediaType.Movie);

		var wantlistedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(wantlistedId.ToExternalIdEntity(wantlistedMedia), wantlistedImdb.ToExternalIdEntity(wantlistedMedia));
		var metadata = BuildMetadata(wantlistedMedia, "Wantlisted");
		fixture.Context.MediaMetadata.Add(metadata);
		fixture.Context.Wantlist.Add(new WantlistRow { Media = wantlistedMedia, Origin = WantlistItemOrigin.Manual });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.Wantlist.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(wantlistedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(wantlistedId);
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsInOutbox_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var queuedId = PlexId.From("4000", MediaType.Movie);
		var queuedImdb = ImdbId.From("tt4000", MediaType.Movie);

		var queuedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(queuedId.ToExternalIdEntity(queuedMedia), queuedImdb.ToExternalIdEntity(queuedMedia));
		fixture.Context.WantlistOutbox.Add(new WantlistOutboxRow { Media = queuedMedia, Action = WantlistOperationAction.Add });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.WantlistOutbox.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(queuedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(queuedId);
	}

	[Test]
	public async Task PurgeAsync_ProtectsMediaReferencedByAnotherUser()
	{
		var otherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		fixture.Context.Users.Add(
			new UserRow
			{
				Id = otherUserId,
				Provider = AuthProvider.Plex,
				ExternalUserId = otherUserId.ToString(),
				DisplayName = "Other",
				AvatarUrl = string.Empty,
				Role = UserRole.Member,
				JoinedAt = DateTimeOffset.UnixEpoch,
			}
		);

		var sharedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.Add(PlexId.From("9000", MediaType.Movie).ToExternalIdEntity(sharedMedia));
		fixture.Context.MediaMetadata.Add(BuildMetadata(sharedMedia, "Referenced By Other"));
		await fixture.Context.SaveChangesAsync();

		var adminId = fixture.CurrentUser.Id;
		fixture.CurrentUser.Id = UserId.From(otherUserId);
		fixture.Context.Wantlist.Add(new WantlistRow { Media = sharedMedia, Origin = WantlistItemOrigin.Manual });
		await fixture.Context.SaveChangesAsync();
		fixture.CurrentUser.Id = adminId;
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		await Assert.That(await fixture.Context.Media.IgnoreQueryFilters().AnyAsync(m => m.Id == sharedMedia.Id)).IsTrue();
	}

	[Test]
	public async Task PurgeMetadataAsync_WhenNotAdmin_Throws()
	{
		var configRepository = IProviderConfigRepository.Mock();
		var member = new TestCurrentUser { Role = UserRole.Member };
		var testee = new MediaCachePurger(fixture.ContextFactory, configRepository.Object, member, NullLogger<MediaCachePurger>.Instance);

		await Assert.That(async () => await testee.PurgeMetadataAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task PurgeMetadataAsync_DeletesAllMetadataAndRatings_KeepsMediaAndExternalIds()
	{
		var plexId = PlexId.From("5000", MediaType.Movie);
		var imdbId = ImdbId.From("tt5000", MediaType.Movie);

		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(plexId.ToExternalIdEntity(media), imdbId.ToExternalIdEntity(media));
		var metadata = BuildMetadata(media, "Cached");
		fixture.Context.MediaMetadata.Add(metadata);
		fixture.Context.MediaRatings.Add(
			new MediaRatingRow
			{
				Source = RatingProvider.Tmdb,
				NormalizedValue = 8.5f,
				VoteCount = 999,
				Metadata = metadata,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeMetadataAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.MediaMetadata.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.MediaRatings.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.ExternalMediaIds.CountAsync()).IsEqualTo(2);
	}

	[Test]
	public async Task PurgeMetadataAsync_RetainedMediaStillResolvesAsPartial()
	{
		var plexId = PlexId.From("6000", MediaType.Movie);
		var imdbId = ImdbId.From("tt6000", MediaType.Movie);

		var media = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(plexId.ToExternalIdEntity(media), imdbId.ToExternalIdEntity(media));
		fixture.Context.MediaMetadata.Add(BuildMetadata(media, "Cached"));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeMetadataAsync();

		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
		var repository = new MediaRepository(fixture.ContextFactory, new MediaMetadataFactory(dateTimeProvider.Object), new MediaWriteLock());
		var cached = await repository.GetAsync([MediaId.From(media.Id)]);

		using var _ = Assert.Multiple();
		await Assert.That(cached.Count).IsEqualTo(1);
		await Assert.That(cached[0].Stored).IsOfUnionType<IdentifiedMedia, PartialMovie>();
		await Assert.That(cached[0].Stored.Identity.Id).IsEqualTo(MediaId.From(media.Id));
		await Assert.That(cached[0].Stored.Identity.Externals.Count).IsEqualTo(2);
		await Assert.That(cached[0].FetchedAt).IsNull();
	}

	private static MovieMetadataRow BuildMetadata(MediaRow media, string title) =>
		new()
		{
			Media = media,
			Title = title,
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};
}
