using Contracts.Datasets;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Datasets;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DatasetCheckRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 4, 30, 0, TimeSpan.Zero);

	private static readonly DatasetCheck Loaded = new(DatasetNames.ImdbRatings, Now, Now.AddSeconds(3), DatasetOutcome.Loaded, new None());

	private IDatasetChecks Testee => host.Resolve<IDatasetChecks>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RecordAsync_Twice_KeepsOnlyTheLastCheckOfThatDataset()
	{
		var failed = new DatasetCheck(
			DatasetNames.ImdbRatings,
			Now.AddDays(1),
			Now.AddDays(1),
			DatasetOutcome.Failed,
			Some.Of("datasets.imdbws.com answered 503.")
		);
		await Testee.RecordAsync(Loaded);

		await Testee.RecordAsync(failed);

		await Assert.That(await Testee.FindAsync(DatasetNames.ImdbRatings)).IsSome().And.IsEqualTo(failed);
	}

	[Test]
	public async Task FindAsync_ForADatasetOnlyAnotherWasCheckedOf_FindsNothing()
	{
		await Testee.RecordAsync(Loaded);

		await Assert.That(await Testee.FindAsync(DatasetNames.AnimeIds)).IsNone();
		await Assert.That(await Testee.FindAsync(DatasetNames.ImdbRatings)).IsSome().And.IsEqualTo(Loaded);
	}
}
