namespace Contracts.Housekeeping;

public interface IDatabaseFile
{
	Task<Compaction> CompactAsync(CancellationToken cancellationToken = default);
}

public sealed record Compaction(long BytesBefore, long BytesAfter, bool Vacuumed);
