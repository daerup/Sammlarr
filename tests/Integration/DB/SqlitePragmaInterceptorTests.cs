using Contracts.Datasets;
using Harness;

namespace Integration.Tests.DB;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SqlitePragmaInterceptorTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const long JournalSizeLimit = 8 * 1024 * 1024;

	private IDatasetLoads Testee => host.Resolve<IDatasetLoads>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RecordAsync_AfterACheckpointedLargeWrite_TruncatesTheWriteAheadLogToItsLimit()
	{
		await ExecuteAsync(
			"CREATE TABLE Ballast (Bytes BLOB); WITH RECURSIVE Counter(N) AS (SELECT 1 UNION ALL SELECT N + 1 FROM Counter WHERE N < 1500) INSERT INTO Ballast SELECT randomblob(8192) FROM Counter;"
		);
		var grown = Length(WriteAheadLog);
		await ExecuteAsync("PRAGMA wal_checkpoint(PASSIVE)");

		await Testee.RecordAsync(new DatasetLoad(DatasetNames.ImdbRatings, DateTimeOffset.UnixEpoch, new None(), 1));

		await Assert.That(grown).IsGreaterThan(JournalSizeLimit);
		await Assert.That(Length(WriteAheadLog)).IsLessThanOrEqualTo(JournalSizeLimit);
	}

	private string WriteAheadLog => $"{host.Connection.DataSource}-wal";

	private static long Length(string path) => new FileInfo(path) is { Exists: true } file ? file.Length : 0;

	private async Task ExecuteAsync(string sql)
	{
		await using var command = host.Connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}
}
