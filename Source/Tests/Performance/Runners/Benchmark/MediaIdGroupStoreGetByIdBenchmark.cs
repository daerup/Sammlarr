using BenchmarkDotNet.Attributes;
using Contracts.Media;
using DB;
using Microsoft.EntityFrameworkCore;
using Performance.Runners.Benchmark.Old;
using Shared;

namespace Performance.Runners.Benchmark;

public class MediaIdGroupStoreGetByIdBenchmark()
{
	internal static string? ConfiguredDbPath { get; set; }
	internal static int? ConfiguredSeed { get; set; }

	private readonly List<IMediaId> _lookupIds = [];
	private SammlarrDbContext _dbContext = null!;
	private MediaIdGroupRepository _repository = null!;
	private MediaIdGroupRepository _oldRepository = null!;

	[GlobalSetup]
	public async Task SetupAsync()
	{
		var (dbPath, seed) = (@"C:\Repos\Sammlarr\Source\artifacts\perf\performance.db", 1604981);
		_dbContext = PerformanceDbContextFactory.Create(dbPath);
		var rng = new Random(seed);
		var count = await _dbContext.MediaIdGroups.CountAsync();
		if (count is 0)
		{
			throw new InvalidOperationException("Benchmark database does not contain media ID groups.");
		}
		var skip = rng.Next(count);
		var randomGroup = await _dbContext.MediaIdGroups.Include(group => group.MediaIdEntries).Skip(skip).FirstAsync();
		_lookupIds.AddRange(randomGroup.MediaIdEntries.Select(entry => entry.Identifier.ToMediaId()));

		_repository = new MediaIdGroupRepository(_dbContext);
		_oldRepository = new MediaIdGroupRepository(_dbContext);
		_dbContext.ChangeTracker.Clear();
	}

	[Benchmark(Description = "New.GetByIdAsync")]
	public async Task GetByIdExistingAsync()
	{
		await RunExistingBenchmarkAsync(_repository);
		await RunNonExistingBenchmarkAsync(_repository);
	}

	[Benchmark(Description = "Old.GetByIdAsync", Baseline = true)]
	public async Task GetByIdExistingOldAsync()
	{
		await RunExistingBenchmarkAsync(_oldRepository);
		await RunNonExistingBenchmarkAsync(_oldRepository);
	}

	private async Task RunExistingBenchmarkAsync<TStore>(TStore store)
		where TStore : IMediaIdGroupRepository
	{
		var result = await _lookupIds.ToAsyncEnumerable().SelectAwait(async lookupId => await store.GetByIdAsync(lookupId)).ToListAsync();
		if (result.Count == 0)
		{
			throw new InvalidOperationException("Expected at least one mapped ID, but got none.");
		}
	}

	private async Task RunNonExistingBenchmarkAsync<TStore>(TStore store)
		where TStore : IMediaIdGroupRepository
	{
		var lookupId = ImdbId.From("ttsome nonexistent id", MediaType.Movie);
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
