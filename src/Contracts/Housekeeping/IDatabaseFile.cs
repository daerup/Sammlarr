namespace Contracts.Housekeeping;

public interface IDatabaseFile
{
	Task<Compaction> CompactAsync(CancellationToken cancellationToken = default);

	Task<DatabaseSize> SizeAsync(CancellationToken cancellationToken = default);

	Task<DatabaseParts> PartsAsync(CancellationToken cancellationToken = default);
}

public sealed record Compaction(long BytesBefore, long BytesAfter, bool Vacuumed);

public sealed record DatabaseSize(long OnDisk, long Data);

public sealed record DatabaseParts(long ImdbRatings, long AnimeIds, long FeedHistory);
