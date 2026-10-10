using Contracts.Media;
using Core.Media;
using Domain.Media;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Media;

public class MediaWriteBufferTests
{
	private static readonly DateTimeOffset RefreshedAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
	private static readonly MediaIdentity Survivor = new(MediaId.New(), [TmdbId.From("1", MediaType.Movie)]);
	private static readonly MediaIdentity Absorbed = new(MediaId.New(), [ImdbId.From("tt1", MediaType.Movie)]);

	private readonly Mock<IMediaRepository> _repository = Repository();

	[Test]
	public async Task DisposeAsync_WhenTwoTitlesWithDetailsMergeIntoOne_FlushesOneDetailsRowForTheSurvivor()
	{
		await using (var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance))
		{
			testee.Add(new MatchedMedia(Survivor, new MovieDetails { Title = "Survivor" }), Cached("Survivor"));
			testee.Add(new MatchedMedia(Absorbed, new MovieDetails { Title = "Absorbed" }), Cached("Absorbed"));
			testee.MergeIdentity(Absorbed.Id, Survivor with { Keys = Survivor.Keys + Absorbed.Keys });
		}

		_repository
			.UpsertDetailsAsync(Is<IReadOnlyDictionary<MediaId, CachedDetails>>(details => details is { Count: 1 } && details.ContainsKey(Survivor.Id)), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task DisposeAsync_WhenTheLookupReportedMoreIds_PersistsThemWithTheIdentity()
	{
		var plexOnly = new MediaIdentity(MediaId.New(), [PlexId.From("plex-1", MediaType.Movie)]);
		var learned = TmdbId.From("27205", MediaType.Movie);

		await using (var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance))
		{
			testee.Add(new MatchedMedia(plexOnly with { Keys = plexOnly.Keys + [learned] }, new MovieDetails()), Cached("Inception"));
		}

		_repository
			.UpsertIdentitiesAsync(Is<IReadOnlyList<MediaIdentity>>(identities => identities is [var only] && only.Keys.Contains(learned)), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task AddIdentity_AfterItsIdWasMerged_LandsOnTheSurvivor()
	{
		var learned = PlexId.From("plex-1", MediaType.Movie);
		await using var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance);
		testee.MergeIdentity(Absorbed.Id, Survivor with { Keys = Survivor.Keys + Absorbed.Keys });

		testee.AddIdentity(Absorbed with { Keys = Absorbed.Keys + [learned] });

		await Assert.That(testee.NewIdentities.Select(identity => identity.Id)).IsEquivalentTo([Survivor.Id]);
		await Assert.That(testee.NewIdentities.Single().Keys).Contains(learned);
	}

	[Test]
	public async Task Add_AfterItsIdWasMerged_KeepsTheDetailsOnTheSurvivor()
	{
		await using var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance);
		testee.MergeIdentity(Absorbed.Id, Survivor with { Keys = Survivor.Keys + Absorbed.Keys });

		testee.Add(new MatchedMedia(Absorbed, new MovieDetails { Title = "Late" }), Cached("Late"));

		await Assert.That(testee.NewDetails.Keys).IsEquivalentTo([Survivor.Id]);
		await Assert.That(testee.NewIdentities.Select(identity => identity.Id)).IsEquivalentTo([Survivor.Id]);
	}

	[Test]
	public async Task MergeIdentity_BackAndForthBetweenTwoIds_KeepsOneIdentity()
	{
		var both = Survivor.Keys + Absorbed.Keys;
		await using var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance);
		testee.MergeIdentity(Absorbed.Id, Survivor with { Keys = both });

		testee.MergeIdentity(Survivor.Id, Absorbed with { Keys = both });

		var absorbedRoot = await Task.Run(() => testee.Survivor(Absorbed.Id)).WaitAsync(TimeSpan.FromSeconds(5));
		var survivorRoot = await Task.Run(() => testee.Survivor(Survivor.Id)).WaitAsync(TimeSpan.FromSeconds(5));

		await Assert.That(testee.NewIdentities).Count().IsEqualTo(1);
		await Assert.That(absorbedRoot).IsEqualTo(survivorRoot);
	}

	[Test]
	public async Task MergeIdentity_AfterTheAbsorbedIdLearnedAnId_KeepsItOnTheSurvivor()
	{
		var learned = PlexId.From("plex-2", MediaType.Movie);
		await using var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance);
		testee.AddIdentity(Absorbed with { Keys = Absorbed.Keys + [learned] });

		testee.MergeIdentity(Absorbed.Id, Survivor with { Keys = Survivor.Keys + Absorbed.Keys });

		await Assert.That(testee.NewIdentities.Single().Keys).Contains(learned);
	}

	[Test]
	public async Task AddIdentity_FromManyThreadsAtOnce_KeepsEveryIdentity()
	{
		await using var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance);

		Parallel.For(0, 2000, i => testee.AddIdentity(new MediaIdentity(MediaId.New(), [TmdbId.From($"{i}", MediaType.Movie)])));

		await Assert.That(testee.NewIdentities).Count().IsEqualTo(2000);
	}

	private static CachedDetails Cached(string title) => new(new MovieDetails { Title = title }, RefreshedAt, RefreshedAt, []);

	private static Mock<IMediaRepository> Repository()
	{
		var repository = IMediaRepository.Mock();
		repository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		return repository;
	}
}
