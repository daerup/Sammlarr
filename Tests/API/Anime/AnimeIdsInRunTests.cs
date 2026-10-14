using API.Anime;
using Contracts.Datasets;
using Domain.Media;
using TestInfrastructure;

namespace API.Tests.Anime;

public class AnimeIdsInRunTests
{
	private static readonly AniListId Apothecary = AniListId.From("161645", MediaType.Show);

	[Test]
	public async Task PrimeAsync_ThenFindAsync_AnswersThePrimedEntryWhateverTheMapSaysNow()
	{
		var map = AnimeMaps.KnowingTheApothecaryDiaries();
		var testee = new AnimeIdsInRun(map.Object, AnimeMaps.Loads(DatasetNames.AnimeIds).Object);
		await testee.PrimeAsync(
			[
				[Apothecary],
			],
			CancellationToken.None
		);
		map.FindAsync(Any(), Any()).Returns(new None());

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		await Assert.That(found).IsSuccess().And.IsEqualTo(Some.Of(AnimeMaps.ApothecaryDiaries));
	}

	[Test]
	public async Task FindAsync_BeforeTheMapIsLoaded_FailsAndSucceedsNextTimeOnceItLoaded()
	{
		var loads = AnimeMaps.Loads(DatasetNames.AnimeIds, loaded: false);
		var testee = new AnimeIdsInRun(AnimeMaps.KnowingTheApothecaryDiaries().Object, loads.Object);
		var unloaded = await testee.FindAsync(Apothecary, CancellationToken.None);
		loads.FindAsync(Any(), Any()).Returns(Some.Of(new DatasetLoad(DatasetNames.AnimeIds, DateTimeOffset.UnixEpoch, new None(), 1)));

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		await Assert.That(unloaded).IsError();
		await Assert.That(found).IsSuccess().And.IsEqualTo(Some.Of(AnimeMaps.ApothecaryDiaries));
	}

	[Test]
	public async Task PrimeAsync_ForAnIdTheMapLacks_RemembersTheMissOnceTheMapKnowsTheId()
	{
		var map = AnimeMaps.Unknown();
		var testee = new AnimeIdsInRun(map.Object, AnimeMaps.Loads(DatasetNames.AnimeIds).Object);
		await testee.PrimeAsync(
			[
				[Apothecary],
			],
			CancellationToken.None
		);
		map.FindAsync(Any(), Any()).Returns(Some.Of(AnimeMaps.ApothecaryDiaries));

		var found = await testee.FindAsync(Apothecary, CancellationToken.None);

		var entry = await Assert.That(found).IsSuccess();
		await Assert.That(entry!).IsNone();
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
	}
}
