using Contracts.Media;
using Contracts.Media.Metadata;
using Core.Media;

namespace Core.Tests.Unit.Media;

public class MediaIdentityCoordinatorTests
{
	[Test]
	public async Task CoordinateAsync_WhenTwoPreliminaryShareExternal_MergesIntoOneCanonicalId()
	{
		var tmdbA = TmdbId.From("1", MediaType.Movie);
		var tmdbB = TmdbId.From("2", MediaType.Movie);
		var sharedImdb = ImdbId.From("tt0001", MediaType.Movie);
		var (coordinator, buffer) = CreateCoordinator();

		var corrected = await coordinator.CoordinateAsync(
			[new(MediaId.From(Guid.NewGuid()), [tmdbA, sharedImdb]), new(MediaId.From(Guid.NewGuid()), [tmdbB, sharedImdb])],
			buffer,
			CancellationToken.None
		);

		await Assert.That(corrected[0].Id).IsEqualTo(corrected[1].Id);
		await Assert.That(corrected[0].Externals).Contains(tmdbA);
		await Assert.That(corrected[0].Externals).Contains(tmdbB);
		await Assert.That(corrected[0].Externals).Contains(sharedImdb);
	}

	[Test]
	public async Task CoordinateAsync_WhenTwoPreliminaryShareExternal_AddsOneIdentityToBuffer()
	{
		var tmdbA = TmdbId.From("10", MediaType.Movie);
		var tmdbB = TmdbId.From("11", MediaType.Movie);
		var sharedImdb = ImdbId.From("tt0010", MediaType.Movie);
		var (coordinator, buffer) = CreateCoordinator();

		await coordinator.CoordinateAsync(
			[new(MediaId.From(Guid.NewGuid()), [tmdbA, sharedImdb]), new(MediaId.From(Guid.NewGuid()), [tmdbB, sharedImdb])],
			buffer,
			CancellationToken.None
		);

		await Assert.That(buffer.NewIdentities.DistinctBy(i => i.Id).Count()).IsEqualTo(1);
	}

	[Test]
	public async Task CoordinateAsync_WhenExistingDbRowSharesExternalWithPreliminary_MergesOntoExistingId()
	{
		var tmdbExisting = TmdbId.From("20", MediaType.Movie);
		var sharedImdb = ImdbId.From("tt0020", MediaType.Movie);
		var tmdbNew = TmdbId.From("21", MediaType.Movie);
		var existingId = MediaId.From(Guid.NewGuid());
		var existingIdentity = new MediaIdentity(existingId, [tmdbExisting]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindManyAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, tmdbExisting, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync(
			[new(MediaId.From(Guid.NewGuid()), [tmdbExisting, sharedImdb]), new(MediaId.From(Guid.NewGuid()), [tmdbNew, sharedImdb])],
			buffer,
			CancellationToken.None
		);

		await Assert.That(corrected[0].Id).IsEqualTo(existingId);
		await Assert.That(corrected[1].Id).IsEqualTo(existingId);
	}

	[Test]
	public async Task CoordinateAsync_WhenExternalUnknown_MintsNewIdentity()
	{
		var external = TmdbId.From("9001", MediaType.Movie);
		var (coordinator, buffer) = CreateCoordinator();
		var preliminary = new List<MediaIdentity> { new(MediaId.From(Guid.NewGuid()), [external]) };

		var corrected = await coordinator.CoordinateAsync(preliminary, buffer, CancellationToken.None);

		await Assert.That(corrected.Count).IsEqualTo(1);
		await Assert.That(corrected[0].Externals).Contains(external);
		await Assert.That(buffer.NewIdentities.Count).IsEqualTo(1);
	}

	[Test]
	public async Task CoordinateAsync_WhenExternalKnown_ReusesPersistedMediaId()
	{
		var external = TmdbId.From("9002", MediaType.Movie);
		var existingId = MediaId.From(Guid.NewGuid());
		var existingIdentity = new MediaIdentity(existingId, [external]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindManyAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, external, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync([new(MediaId.From(Guid.NewGuid()), [external])], buffer, CancellationToken.None);

		await Assert.That(corrected[0].Id).IsEqualTo(existingId);
	}

	[Test]
	public async Task CoordinateAsync_WhenDbMatchHasOverlap_UnionsExternals()
	{
		var sharedTmdb = TmdbId.From("9003", MediaType.Movie);
		var dbImdb = ImdbId.From("tt9003", MediaType.Movie);
		var freshPlex = PlexId.From("plex-9003", MediaType.Movie);
		var existingId = MediaId.From(Guid.NewGuid());
		var existingIdentity = new MediaIdentity(existingId, [sharedTmdb, dbImdb]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindManyAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, sharedTmdb, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync([new(MediaId.From(Guid.NewGuid()), [sharedTmdb, freshPlex])], buffer, CancellationToken.None);

		await Assert.That(corrected[0].Id).IsEqualTo(existingId);
		await Assert.That(corrected[0].Externals).Contains(sharedTmdb);
		await Assert.That(corrected[0].Externals).Contains(dbImdb);
		await Assert.That(corrected[0].Externals).Contains(freshPlex);
	}

	private static (MediaIdentityCoordinator Coordinator, MediaResolutionBuffer Buffer) CreateCoordinator(Action<Mock<IMediaRepository>>? setup = null)
	{
		var mediaRepo = IMediaRepository.Mock();
		mediaRepo.FindManyAsync(Any(), Any()).Returns((groups, _) => new MediaIdentity?[groups.Count]);
		setup?.Invoke(mediaRepo);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		var buffer = new MediaResolutionBuffer(mediaRepo.Object, IMediaMetadataRepository.Mock().Object);
		return (coordinator, buffer);
	}

	private static MediaIdentity?[] MatchGroups(IReadOnlyList<IReadOnlyList<IExternalMediaId>> groups, IExternalMediaId key, MediaIdentity result) =>
		[.. groups.Select(g => g.Any(e => e.Source == key.Source && e.Value == key.Value) ? (MediaIdentity?)result : null)];
}
