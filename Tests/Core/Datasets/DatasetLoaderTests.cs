using Contracts;
using Contracts.Datasets;
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

	public DatasetLoaderTests()
	{
		_dataset.Name.Returns(DatasetNames.ImdbRatings);
		_loads.FindAsync(DatasetNames.ImdbRatings, Any()).Returns(Some.Of(Yesterday));
	}

	[Test]
	public async Task LoadAsync_DownloadsUnderTheDatasetCause()
	{
		var cause = UsageCause.Other;
		_dataset
			.DownloadAsync(Any(), Any())
			.Returns(Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(new None()))
			.Callback(() => cause = UsageCauses.Current.Cause);

		await Testee().LoadAsync();

		await Assert.That(cause).IsEqualTo(UsageCause.Dataset);
	}

	[Test]
	public async Task LoadAsync_WhenTheFileIsUnchanged_LoadsAndRecordsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Success.Of<Optional<DatasetFile<ImdbRatingLine>>>(new None()));

		var loaded = await Testee().LoadAsync();

		await Assert.That(loaded).IsSuccess().And.IsEqualTo(0);
		_table.ReplaceAsync(Any(), Any()).WasNeverCalled();
		_loads.RecordAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_ForAChangedFile_AsksWithTheLastETagReplacesTheTableAndRecordsTheLoad()
	{
		_dataset.DownloadAsync((Optional<string>)Some.Of("\"yesterday\""), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Returns(2);

		var loaded = await Testee().LoadAsync();

		await Assert.That(loaded).IsSuccess().And.IsEqualTo(2);
		_loads.RecordAsync(new DatasetLoad(DatasetNames.ImdbRatings, Now, Some.Of("\"today\""), 2), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadAsync_WhenTheTableCannotTakeTheRows_RecordsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Changed());
		_table.ReplaceAsync(Any(), Any()).Throws(new InvalidDataException("The file is cut short."));

		var loaded = await Testee().LoadAsync();

		await Assert.That(loaded).IsError();
		_loads.RecordAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadAsync_WhenTheDownloadFails_RecordsNothing()
	{
		_dataset.DownloadAsync(Any(), Any()).Returns(Error.From("IMDb's datasets answered 500 for the ratings file."));

		var loaded = await Testee().LoadAsync();

		await Assert.That(loaded).IsError();
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

		var first = Testee(gate).LoadAsync();
		var second = Testee(gate).LoadAsync();

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
			clock.Object,
			gate ?? new DatasetGate(),
			NullLogger<DatasetLoader<ImdbRatingLine>>.Instance
		);
	}
}
