using Contracts;
using Contracts.Datasets;
using Contracts.Housekeeping;
using Core.Datasets;
using Domain.Usage;
using Microsoft.Extensions.Logging.Abstractions;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Datasets;

public class DatasetLoaderTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 6, 4, 30, 0, TimeSpan.Zero);

	private static readonly DatasetLoad Yesterday = new(DatasetNames.ImdbRatings, Now.AddDays(-1), Some.Of("\"yesterday\""), 2);

	private static readonly ImdbRatingLine[] Lines = [new("tt0137523", 8.8f, 2_669_240), new("tt0903747", 9.5f, 2_686_170)];

	private readonly Mock<IDataset<ImdbRatingLine>> _dataset = IDataset<ImdbRatingLine>.Mock();

	private readonly Mock<IDatasetTable<ImdbRatingLine>> _table = IDatasetTable<ImdbRatingLine>.Mock();

	private readonly Mock<IDatasetLoads> _loads = IDatasetLoads.Mock();

	private readonly Mock<IDatabaseFile> _database = IDatabaseFile.Mock();

	public DatasetLoaderTests()
	{
		_dataset.Name.Returns(DatasetNames.ImdbRatings);
		_loads.FindAsync(DatasetNames.ImdbRatings, Any()).Returns(Some.Of(Yesterday));
		_database.CompactAsync(Any()).Returns(new Compaction(100, 50, true));
	}

	[Test]
	public async Task LoadAsync_DownloadsUnderTheDatasetCause()
	{
		var cause = UsageCause.Other;
		_dataset
			.DownloadAsync(Any(), Any())
			.Returns(Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(new None()))
			.Callback(() => cause = UsageCauses.Current.Cause);

		await Testee().LoadAsync(DatasetRead.WhenChanged);

		await Assert.That(cause).IsEqualTo(UsageCause.Dataset);
	}

	[Test]
	public async Task LoadAsync_WhenTheFileIsUnchanged_SaysSoAndLoadsRecordsAndCompactsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(new None()));

		var loaded = await Testee().LoadAsync(DatasetRead.WhenChanged);

		await Assert.That(loaded).IsEqualTo(new DatasetCheck(DatasetNames.ImdbRatings, Now, Now, DatasetOutcome.Unchanged, new None()));
		_table.ReplaceAsync(Any(), Any()).WasNeverCalled();
		_loads.RecordAsync(Any(), Any()).WasNeverCalled();
		_database.CompactAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ForAChangedFile_AsksWithTheLastETagReplacesTheTableAndRecordsTheLoad()
	{
		_dataset.DownloadAsync((Optional<string>)Some.Of("\"yesterday\""), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Returns(2);

		var loaded = await Testee().LoadAsync(DatasetRead.WhenChanged);

		await Assert.That(loaded).IsEqualTo(new DatasetCheck(DatasetNames.ImdbRatings, Now, Now, DatasetOutcome.Loaded, new None()));
		_loads.RecordAsync(new DatasetLoad(DatasetNames.ImdbRatings, Now, Some.Of("\"today\""), 2), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_Always_AsksForTheWholeFileDespiteTheStoredETag()
	{
		_dataset.DownloadAsync((Optional<string>)new None(), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Returns(2);

		var loaded = await Testee().LoadAsync(DatasetRead.Always);

		await Assert.That(loaded).IsEqualTo(new DatasetCheck(DatasetNames.ImdbRatings, Now, Now, DatasetOutcome.Loaded, new None()));
	}

	[Test]
	public async Task LoadAsync_ForAChangedFile_CompactsTheDatabaseFileOnceTheLoadIsRecorded()
	{
		var recorded = false;
		var compactedAfterRecord = false;
		_dataset.DownloadAsync(Any(), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Returns(2);
		_loads.RecordAsync(Any(), Any()).Callback(() => recorded = true);
		_database.CompactAsync(Any()).Returns(new Compaction(100, 50, true)).Callback(() => compactedAfterRecord = recorded);

		await Testee().LoadAsync(DatasetRead.WhenChanged);

		_database.CompactAsync(Any()).WasCalled(Times.Once);
		await Assert.That(compactedAfterRecord).IsTrue();
	}

	[Test]
	public async Task LoadAsync_WhenTheTableCannotTakeTheRows_FailsWithWhyAndRecordsAndCompactsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Throws(new InvalidDataException("The file is cut short."));

		var loaded = await Testee().LoadAsync(DatasetRead.WhenChanged);

		await Assert.That(loaded.Outcome).IsEqualTo(DatasetOutcome.Failed);
		await Assert.That(loaded.Failure).IsSome().And.IsEqualTo("The file is cut short.");
		_loads.RecordAsync(Any(), Any()).WasNeverCalled();
		_database.CompactAsync(Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_WhenTheDownloadFails_FailsWithWhyAndRecordsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Error.From("IMDb's datasets answered 500 for the ratings file."));

		var loaded = await Testee().LoadAsync(DatasetRead.WhenChanged);

		await Assert.That(loaded.Outcome).IsEqualTo(DatasetOutcome.Failed);
		await Assert.That(loaded.Failure).IsSome().And.IsEqualTo("IMDb's datasets answered 500 for the ratings file.");
		_table.ReplaceAsync(Any(), Any()).WasNeverCalled();
		_loads.RecordAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_WhileAnotherLoadRuns_WaitsForItBeforeDownloading()
	{
		var replacing = new TaskCompletionSource<int>();
		_dataset.DownloadAsync(Any(), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).ReturnsAsync(replacing.Task);
		var gate = new DatasetGate();

		var first = Testee(gate).LoadAsync(DatasetRead.WhenChanged);
		var second = Testee(gate).LoadAsync(DatasetRead.WhenChanged);

		_dataset.DownloadAsync(Any(), Any()).WasCalled(Times.Once);
		replacing.SetResult(2);
		await Task.WhenAll(first, second);
		_dataset.DownloadAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	private static Result<Optional<DatasetFile<ImdbRatingLine>>> Changed()
	{
		return Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(Some.Of(new DatasetFile<ImdbRatingLine>(Some.Of("\"today\""), Lines.ToAsyncEnumerable())));
	}

	private DatasetLoader<ImdbRatingLine> Testee(DatasetGate? gate = null)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(Now);
		return new DatasetLoader<ImdbRatingLine>(
			_dataset.Object,
			_table.Object,
			_loads.Object,
			_database.Object,
			clock.Object,
			gate ?? new DatasetGate(),
			NullLogger<DatasetLoader<ImdbRatingLine>>.Instance
		);
	}
}
