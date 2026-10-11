using Contracts.Datasets;
using Contracts.Housekeeping;
using Harness;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DatabaseFileTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const long JournalSizeLimit = 8 * 1024 * 1024;

	private IDatabaseFile Testee => host.Resolve<IDatabaseFile>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task CompactAsync_AfterALoadReplacedByASmallerOne_VacuumsTheFileToTheSizeOfItsData()
	{
		var ratings = host.Resolve<IImdbRatings>();
		await ratings.ReplaceAsync(Ratings(300_000));
		await ratings.ReplaceAsync(Ratings(3_000));

		var compaction = await Testee.CompactAsync();

		await Assert.That(compaction.Vacuumed).IsTrue();
		await Assert.That(compaction.BytesAfter).IsLessThan(compaction.BytesBefore);
		await Assert.That(Length(WriteAheadLog)).IsLessThanOrEqualTo(JournalSizeLimit);
		var compacted = await VacuumedCopyLengthAsync();
		await Assert.That(Length(DatabasePath)).IsBetween((long)(compacted * 0.95), (long)(compacted * 1.05));
	}

	[Test]
	public async Task CompactAsync_RightAfterACompaction_LeavesTheFileUnvacuumedWithAnEmptyLog()
	{
		await host.Resolve<IImdbRatings>().ReplaceAsync(Ratings(30_000));
		await Testee.CompactAsync();

		var compaction = await Testee.CompactAsync();

		await Assert.That(compaction.Vacuumed).IsFalse();
		await Assert.That(Length(WriteAheadLog)).IsEqualTo(0);
	}

	[Test]
	public async Task SizeAsync_AfterALoadReplacedByASmallerOne_CountsTheFileAndItsLogAndOnlyTheUsedPagesAsData()
	{
		var ratings = host.Resolve<IImdbRatings>();
		await ratings.ReplaceAsync(Ratings(300_000));
		await ratings.ReplaceAsync(Ratings(3_000));

		var size = await Testee.SizeAsync();

		var compacted = await VacuumedCopyLengthAsync();
		await Assert.That(size.OnDisk).IsEqualTo(Length(DatabasePath) + Length(WriteAheadLog));
		await Assert.That(size.Data).IsBetween((long)(compacted * 0.95), (long)(compacted * 1.05));
		await Assert.That(size.Data).IsLessThan(size.OnDisk);
	}

	private string DatabasePath => host.Connection.DataSource;

	private string WriteAheadLog => $"{DatabasePath}-wal";

	private static long Length(string path) => new FileInfo(path) is { Exists: true } file ? file.Length : 0;

	private static IAsyncEnumerable<ImdbRatingLine> Ratings(int count)
	{
		return Enumerable.Range(1, count).Select(index => new ImdbRatingLine($"tt{index:D8}", 7.5f, index)).ToAsyncEnumerable();
	}

	private async Task<long> VacuumedCopyLengthAsync()
	{
		var copy = Path.Combine(Path.GetTempPath(), $"sammlarr-vacuumed-{Guid.NewGuid():N}.db");
		try
		{
			await using var command = host.Connection.CreateCommand();
			command.CommandText = "VACUUM INTO $path";
			command.Parameters.AddWithValue("$path", copy);
			await command.ExecuteNonQueryAsync();
			return Length(copy);
		}
		finally
		{
			File.Delete(copy);
		}
	}
}
