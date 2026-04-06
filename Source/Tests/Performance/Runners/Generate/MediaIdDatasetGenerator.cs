using Contracts.Media;
using DB;
using DB.Data;
using DB.Data.Entities;
using Performance.Options;

namespace Performance.Runners.Generate;

internal sealed class MediaIdDatasetGenerator(GenerateModeOptions options)
{
	private const double CompleteGroupProbability = 0.55;
	private const double MissingGroupProbability = 0.20;
	private const double MissingDuplicatesGroupProbability = 0.15;

	private const int MissingGroupContainsAtLeast = 2;
	private const int DuplicatesMinDuplicatedSources = 1;
	private const int DuplicatesMaxDuplicatedSources = 3;
	private const int DuplicatesMinCopiesPerSource = 1;
	private const int DuplicatesMaxCopiesPerSource = 4;

	private const int ImdbIdNumericPaddingWidth = 9;
	private const int PlexIdNumericPaddingWidth = 9;

	private readonly Random _random = new(options.Seed);
	private int _nextTmdbId;
	private int _nextTvdbId;
	private int _nextImdbId;
	private int _nextPlexId;

	public async Task GenerateAsync()
	{
		var startedAt = DateTimeOffset.UtcNow;
		var fullPath = Path.GetFullPath(options.DbPath);
		Console.WriteLine(
			$"[Generate] Starting dataset generation. Path='{fullPath}', EntryCount={options.EntryCount}, BatchSize={options.BatchSize}, Seed={options.Seed}, Overwrite={options.Overwrite}"
		);

		EnsureDatabasePath(fullPath);

		await using var dbContext = PerformanceDbContextFactory.Create(fullPath);
		await dbContext.Database.EnsureCreatedAsync();
		Console.WriteLine("[Generate] Database is ready.");

		var pendingGroups = new List<MediaIdGroup>(options.BatchSize);
		var savedGroups = 0;

		foreach (var _ in Enumerable.Range(0, options.EntryCount))
		{
			pendingGroups.Add(CreateGroup(SelectGroupType()));

			if (pendingGroups.Count >= options.BatchSize)
			{
				savedGroups += await SaveBatchAsync(dbContext, pendingGroups);
				Console.WriteLine($"[Generate] Progress: {savedGroups}/{options.EntryCount} groups saved ({savedGroups * 100.0 / options.EntryCount:F1}%).");
			}
		}

		if (pendingGroups.Count > 0)
		{
			savedGroups += await SaveBatchAsync(dbContext, pendingGroups);
		}

		var duration = DateTimeOffset.UtcNow - startedAt;
		Console.WriteLine($"[Generate] Completed. Saved {savedGroups} groups in {duration.TotalSeconds:F1}s.");
	}

	private MediaIdGroup CreateGroup(GroupType groupType)
	{
		var group = new MediaIdGroup();
		var ids = groupType switch
		{
			GroupType.Complete => GenerateCompleteGroup(),
			GroupType.Missing => GenerateMissingGroup(),
			GroupType.Duplicates => GenerateDuplicateGroup(),
			GroupType.MissingDuplicates => GenerateMissingDuplicatesGroup(),
		};

		group.MediaIds = [.. ids.Select(id => id.MapEntity() with { Group = group })];
		return group;
	}

	private GroupType SelectGroupType()
	{
		return _random.NextDouble() switch
		{
			< CompleteGroupProbability => GroupType.Complete,
			< CompleteGroupProbability + MissingGroupProbability => GroupType.Missing,
			< CompleteGroupProbability + MissingGroupProbability + MissingDuplicatesGroupProbability => GroupType.MissingDuplicates,
			_ => GroupType.Duplicates,
		};
	}

	private IMediaId[] GenerateCompleteGroup() => [CreateTmdbId(), CreateImdbId(), CreateTvdbId(), CreatePlexId()];

	private IMediaId[] GenerateMissingGroup()
	{
		var ids = GenerateCompleteGroup().OrderBy(_ => _random.Next()).ToArray();
		var count = _random.Next(MissingGroupContainsAtLeast, ids.Length);
		return ids[..count];
	}

	private IMediaId[] GenerateDuplicateGroup()
	{
		var baseIds = GenerateCompleteGroup().OrderBy(_ => _random.Next()).ToList();
		var candidateSources = baseIds.Select(id => id.Source).Distinct().OrderBy(_ => _random.Next()).ToList();
		var sourceCount = _random.Next(DuplicatesMinDuplicatedSources, DuplicatesMaxDuplicatedSources + 1);
		sourceCount = Math.Min(sourceCount, candidateSources.Count);
		var duplicateSources = candidateSources.Take(sourceCount);

		foreach (var source in duplicateSources)
		{
			var copies = _random.Next(DuplicatesMinCopiesPerSource, DuplicatesMaxCopiesPerSource + 1);
			baseIds.AddRange(Enumerable.Range(0, copies).Select(_ => CreateId(source)));
		}

		return [.. baseIds];
	}

	private IMediaId[] GenerateMissingDuplicatesGroup()
	{
		var baseIds = GenerateMissingGroup().ToList();
		var candidateSources = baseIds.Select(id => id.Source).Distinct().OrderBy(_ => _random.Next()).ToList();
		var sourceCount = _random.Next(DuplicatesMinDuplicatedSources, DuplicatesMaxDuplicatedSources + 1);
		sourceCount = Math.Min(sourceCount, candidateSources.Count);
		var duplicateSources = candidateSources.Take(sourceCount);

		foreach (var source in duplicateSources)
		{
			var copies = _random.Next(DuplicatesMinCopiesPerSource, DuplicatesMaxCopiesPerSource + 1);
			baseIds.AddRange(Enumerable.Range(0, copies).Select(_ => CreateId(source)));
		}

		return [.. baseIds];
	}

	private IMediaId CreateId(MediaProvider source) =>
		source switch
		{
			MediaProvider.Tmdb => CreateTmdbId(),
			MediaProvider.Imdb => CreateImdbId(),
			MediaProvider.Tvdb => CreateTvdbId(),
			MediaProvider.Plex => CreatePlexId(),
			_ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
		};

	private TmdbId CreateTmdbId() => TmdbId.From(NextValue(ref _nextTmdbId));

	private TvdbId CreateTvdbId() => TvdbId.From(NextValue(ref _nextTvdbId));

	private ImdbId CreateImdbId() => ImdbId.From($"tt{NextValue(ref _nextImdbId).ToString($"D{ImdbIdNumericPaddingWidth}")}");

	private PlexId CreatePlexId() => PlexId.From($"plex-{NextValue(ref _nextPlexId).ToString($"D{PlexIdNumericPaddingWidth}")}");

	private static int NextValue(ref int counter)
	{
		counter++;
		return counter;
	}

	private static async Task<int> SaveBatchAsync(SammlarrDbContext dbContext, List<MediaIdGroup> pendingGroups)
	{
		var saved = pendingGroups.Count;
		await dbContext.MediaIdGroups.AddRangeAsync(pendingGroups);
		await dbContext.SaveChangesAsync();
		pendingGroups.Clear();
		dbContext.ChangeTracker.Clear();
		return saved;
	}

	private void EnsureDatabasePath(string fullPath)
	{
		var directoryPath = Path.GetDirectoryName(fullPath);
		if (directoryPath is not null)
		{
			Directory.CreateDirectory(directoryPath);
		}

		if (!options.Overwrite)
		{
			return;
		}

		DeleteIfExists(fullPath);
		DeleteIfExists($"{fullPath}-shm");
		DeleteIfExists($"{fullPath}-wal");
	}

	private static void DeleteIfExists(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}
}

internal enum GroupType
{
	Complete,
	Missing,
	Duplicates,
	MissingDuplicates,
}
