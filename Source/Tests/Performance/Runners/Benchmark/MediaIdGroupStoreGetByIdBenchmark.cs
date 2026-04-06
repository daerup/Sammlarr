using BenchmarkDotNet.Attributes;
using Contracts.Media;
using DB;
using DB.Data;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace Performance.Runners.Benchmark;

public class MediaIdGroupStoreGetByIdBenchmark()
{
	internal static string ConfiguredDbPath { get; set; } = null!;
	internal static int ConfiguredSeed { get; set; }

	private readonly List<IMediaId> _lookupIds = [];
	private SammlarrDbContext _dbContext = null!;
	private DB.Data.Stores.MediaIdGroupStore _store = null!;
	private Performance.Runners.Benchmark.Old.MediaIdGroupStore _oldStore = null!;

	[GlobalSetup]
	public async Task SetupAsync()
	{
		_dbContext = PerformanceDbContextFactory.Create(ConfiguredDbPath);
		var rng = new Random(ConfiguredSeed);
		var count = await _dbContext.MediaIdGroups.CountAsync();
		if (count is 0)
		{
			throw new InvalidOperationException("Benchmark database does not contain media ID groups.");
		}
		var skip = rng.Next(count);
		var randomGroup = await _dbContext.MediaIdGroups.Include(group => group.MediaIds).Skip(skip).FirstAsync();
		_lookupIds.AddRange(randomGroup.MediaIds.Select(entry => entry.MapValueObject()));

		_store = new DB.Data.Stores.MediaIdGroupStore(_dbContext);
		_oldStore = new Performance.Runners.Benchmark.Old.MediaIdGroupStore(_dbContext);
	}

	[Benchmark(Description = "MediaIdGroupStore.GetByIdAsync.Existing")]
	public async Task GetByIdExistingAsync()
	{
		await RunExistingBenchmarkAsync(_store);
	}

	// [Benchmark(Description = "MediaIdGroupStore.GetByIdAsync.Missing")]
	// public async Task GetByIdMissingAsync()
	// {
	// 	await RunNonExistingBenchmarkAsync(_store);
	// }

	[Benchmark(Description = "Old.GetByIdAsync.Existing", Baseline = true)]
	public async Task GetByIdExistingOldAsync()
	{
		await RunExistingBenchmarkAsync(_store);
	}

	private async Task RunExistingBenchmarkAsync<TStore>(TStore store)
		where TStore : IMediaIdGroupStore
	{
		var result = await _lookupIds.ToAsyncEnumerable().SelectAwait(async lookupId => await store.GetByIdAsync(lookupId)).ToListAsync();
		if (result.Count == 0)
		{
			throw new InvalidOperationException("Expected at least one mapped ID, but got none.");
		}
	}

	private async Task RunNonExistingBenchmarkAsync<TStore>(TStore store)
		where TStore : IMediaIdGroupStore
	{
		var lookupId = ImdbId.From("ttsome nonexistent id");
		var result = await store.GetByIdAsync(lookupId);
		if (result.Count is not 0)
		{
			throw new InvalidOperationException("Expected no mapped IDs for a missing lookup.");
		}
	}

	[GlobalCleanup]
	public async Task CleanupAsync()
	{
		await _dbContext.DisposeAsync();
	}
}
