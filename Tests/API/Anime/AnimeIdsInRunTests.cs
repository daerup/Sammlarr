using API.Anime;
using Contracts.Datasets;
using Domain.Media;
using TestInfrastructure;

namespace API.Tests.Anime;

public class AnimeIdsInRunTests
{
	private static readonly AniListId Apothecary = AniListId.From("161645", MediaType.Show);

	[Test]
	public async Task PrimeAsync_ThenFindAsync_ReadsTheMapOnce()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		var loads = AnimeMaps.Loads(DatasetNames.AnimeIds);
		var testee = new AnimeIdsInRun(map.Object, loads.Object);
		await testee.PrimeAsync(
			[
				[Apothecary],
			],
			CancellationToken.None
		);

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		await Assert.That(found).IsSuccess().And.IsEqualTo(Some.Of(AnimeMaps.ApothecaryDiaries));
		map.FindManyAsync(Any(), Any()).WasCalled(Times.Once);
		map.FindAsync(Any(), Any()).WasCalled(Times.Never);
		loads.FindAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FindAsync_BeforeTheMapIsLoaded_AsksWhetherItLoadedAgainNextTime()
	{
		var loads = AnimeMaps.Loads(DatasetNames.AnimeIds, loaded: false);
		var testee = new AnimeIdsInRun(AnimeMaps.Unknown().Object, loads.Object);
		await testee.FindAsync(Apothecary, CancellationToken.None);

		var second = await testee.FindAsync(Apothecary, CancellationToken.None);

		await Assert.That(second).IsError();
		loads.FindAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task PrimeAsync_ForAnIdTheMapLacks_RemembersTheMiss()
	{
		var map = AnimeMaps.Unknown();
		var testee = new AnimeIdsInRun(map.Object, AnimeMaps.Loads(DatasetNames.AnimeIds).Object);
		await testee.PrimeAsync(
			[
				[Apothecary],
			],
			CancellationToken.None
		);

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		var entry = await Assert.That(found).IsSuccess();
		await Assert.That(entry!).IsNone();
		map.FindAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FindAsync_AfterAPrimeBeforeTheMapLoaded_ReadsTheMapOnceItLoaded()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		var loads = AnimeMaps.Loads(DatasetNames.AnimeIds, loaded: false);
		var testee = new AnimeIdsInRun(map.Object, loads.Object);
		await testee.PrimeAsync(
			[
				[Apothecary],
			],
			CancellationToken.None
		);
		loads.FindAsync(Any(), Any()).Returns(Some.Of(new DatasetLoad(DatasetNames.AnimeIds, DateTimeOffset.UnixEpoch, new None(), 1)));

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		await Assert.That(found).IsSuccess().And.IsEqualTo(Some.Of(AnimeMaps.ApothecaryDiaries));
		map.FindAsync(Any(), Any()).WasCalled(Times.Once);
	}
}
