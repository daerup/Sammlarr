using Contracts.Media;
using Core.Media;
using Domain.Media;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

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
			[new(MediaId.New(), [tmdbA, sharedImdb]), new(MediaId.New(), [tmdbB, sharedImdb])],
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

		await coordinator.CoordinateAsync([new(MediaId.New(), [tmdbA, sharedImdb]), new(MediaId.New(), [tmdbB, sharedImdb])], buffer, CancellationToken.None);

		await Assert.That(buffer.NewIdentities.DistinctBy(i => i.Id).Count()).IsEqualTo(1);
	}

	[Test]
	public async Task CoordinateAsync_WhenExistingDbRowSharesExternalWithPreliminary_MergesOntoExistingId()
	{
		var tmdbExisting = TmdbId.From("20", MediaType.Movie);
		var sharedImdb = ImdbId.From("tt0020", MediaType.Movie);
		var tmdbNew = TmdbId.From("21", MediaType.Movie);
		var existingId = MediaId.New();
		var existingIdentity = new MediaIdentity(existingId, [tmdbExisting]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, tmdbExisting, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync(
			[new(MediaId.New(), [tmdbExisting, sharedImdb]), new(MediaId.New(), [tmdbNew, sharedImdb])],
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
		var preliminary = new List<MediaIdentity> { new(MediaId.New(), [external]) };

		var corrected = await coordinator.CoordinateAsync(preliminary, buffer, CancellationToken.None);

		await Assert.That(corrected.Count).IsEqualTo(1);
		await Assert.That(corrected[0].Externals).Contains(external);
		await Assert.That(buffer.NewIdentities.Count).IsEqualTo(1);
	}

	[Test]
	public async Task CoordinateAsync_WhenExternalKnown_ReusesPersistedMediaId()
	{
		var external = TmdbId.From("9002", MediaType.Movie);
		var existingId = MediaId.New();
		var existingIdentity = new MediaIdentity(existingId, [external]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, external, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync([new(MediaId.New(), [external])], buffer, CancellationToken.None);

		await Assert.That(corrected[0].Id).IsEqualTo(existingId);
	}

	[Test]
	public async Task CoordinateAsync_WhenDbMatchHasOverlap_UnionsExternals()
	{
		var sharedTmdb = TmdbId.From("9003", MediaType.Movie);
		var dbImdb = ImdbId.From("tt9003", MediaType.Movie);
		var freshPlex = PlexId.From("plex-9003", MediaType.Movie);
		var existingId = MediaId.New();
		var existingIdentity = new MediaIdentity(existingId, [sharedTmdb, dbImdb]);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => MatchGroups(groups, sharedTmdb, existingIdentity))
		);

		var corrected = await coordinator.CoordinateAsync([new(MediaId.New(), [sharedTmdb, freshPlex])], buffer, CancellationToken.None);

		await Assert.That(corrected[0].Id).IsEqualTo(existingId);
		await Assert.That(corrected[0].Externals).Contains(sharedTmdb);
		await Assert.That(corrected[0].Externals).Contains(dbImdb);
		await Assert.That(corrected[0].Externals).Contains(freshPlex);
	}

	[Test]
	public async Task CoordinateHandoffsAsync_WhenOneTitlesIdsSitOnTwoStoredTitles_LeavesThatOneAnErrorAndCoordinatesTheRest()
	{
		var conflicting = AniListId.From("16498", MediaType.Show);
		var fine = TmdbId.From("550", MediaType.Movie);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any())
				.Returns(
					(groups, _) =>
						groups.Any(group => group.Contains(conflicting))
							? throw new MediaIdentityConflictException([MediaId.New(), MediaId.New()], [conflicting])
							: Unknown(groups.Count)
				)
		);
		List<Result<IdentifyHandoff>> handoffs = [Handoff(conflicting), Handoff(fine)];

		var coordinated = await coordinator.CoordinateHandoffsAsync(handoffs, new IdentityMerge(), buffer, CancellationToken.None);

		await Assert.That(coordinated[0]).IsError();
		await Assert.That(coordinated[1]).IsSuccess();
	}

	[Test]
	public async Task CoordinateHandoffAsync_ForTwoTitlesOfOneMediaAtOnce_GivesBothTheSameId()
	{
		var shared = ImdbId.From("tt0042", MediaType.Movie);
		using var bothReading = new Barrier(2);
		var together = 0;
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any())
				.Returns(
					(groups, _) =>
					{
						if (bothReading.SignalAndWait(TimeSpan.FromSeconds(5)))
						{
							Interlocked.Increment(ref together);
						}
						return Unknown(groups.Count);
					}
				)
		);
		var merge = new IdentityMerge();

		var coordinated = await Task.WhenAll(
			Task.Run(() => coordinator.CoordinateHandoffAsync(Handoff(TmdbId.From("42", MediaType.Movie), shared), merge, buffer, CancellationToken.None)),
			Task.Run(() => coordinator.CoordinateHandoffAsync(Handoff(PlexId.From("plex-42", MediaType.Movie), shared), merge, buffer, CancellationToken.None))
		);

		var ids = coordinated.Select(handoff => handoff.ToOptional().Map(match => match.Identity.Id)).WhereSome().Distinct().ToList();
		await Assert.That(together).IsEqualTo(2);
		await Assert.That(coordinated[0]).IsSuccess();
		await Assert.That(coordinated[1]).IsSuccess();
		await Assert.That(ids).Count().IsEqualTo(1);
		await Assert.That(buffer.NewIdentities.Select(identity => identity.Id)).IsEquivalentTo(ids);
	}

	[Test]
	public async Task CoordinateHandoffAsync_ForAStoredMatchOrAnError_PassesItThrough()
	{
		var stored = new MediaIdentity(MediaId.New(), [TmdbId.From("7", MediaType.Movie)]);
		var (coordinator, buffer) = CreateCoordinator();
		Result<IdentifyHandoff> known = Success.Of(new IdentifyHandoff(stored, Some.Of(new StoredMedia(stored, new None()))));
		Result<IdentifyHandoff> failed = Error.From("no provider addresses this title");

		var fromStore = await coordinator.CoordinateHandoffAsync(known, new IdentityMerge(), buffer, CancellationToken.None);
		var fromError = await coordinator.CoordinateHandoffAsync(failed, new IdentityMerge(), buffer, CancellationToken.None);

		await Assert.That(fromStore).IsEqualTo(known);
		await Assert.That(fromError).IsEqualTo(failed);
		await Assert.That(buffer.NewIdentities).IsEmpty();
	}

	[Test]
	public async Task CoordinateHandoffAsync_WhenTheIdsBelongToTwoStoredTitles_ReturnsTheConflictAsTheTitlesError()
	{
		var conflicting = AniListId.From("16498", MediaType.Show);
		var (coordinator, buffer) = CreateCoordinator(repo =>
			repo.FindIdentitiesAsync(Any(), Any()).Returns((_, _) => throw new MediaIdentityConflictException([MediaId.New(), MediaId.New()], [conflicting]))
		);

		var coordinated = await coordinator.CoordinateHandoffAsync(Handoff(conflicting), new IdentityMerge(), buffer, CancellationToken.None);

		await Assert.That(coordinated).IsError();
	}

	private static Result<IdentifyHandoff> Handoff(params IExternalMediaId[] externals)
	{
		return Success.Of(new IdentifyHandoff(new MediaIdentity(MediaId.New(), [.. externals]), new None()));
	}

	private static (MediaIdentityCoordinator Coordinator, MediaWriteBuffer Buffer) CreateCoordinator(Action<Mock<IMediaRepository>>? setup = null)
	{
		var mediaRepo = IMediaRepository.Mock();
		mediaRepo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => Unknown(groups.Count));
		setup?.Invoke(mediaRepo);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object, NullLogger<MediaIdentityCoordinator>.Instance);
		var buffer = new MediaWriteBuffer(mediaRepo.Object, NullLogger<MediaWriteBuffer>.Instance);
		return (coordinator, buffer);
	}

	private static IReadOnlyList<Optional<MediaIdentity>> MatchGroups(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> groups,
		IExternalMediaId key,
		MediaIdentity result
	)
	{
		return [.. groups.Select(group => Optional.When(group.Any(e => e.Source == key.Source && e.Value == key.Value), () => result))];
	}

	private static IReadOnlyList<Optional<MediaIdentity>> Unknown(int count)
	{
		return [.. Enumerable.Repeat<Optional<MediaIdentity>>(new None(), count)];
	}
}
