using System.Data.Common;
using Contracts.Housekeeping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DB.Data.Housekeeping;

internal sealed class DatabaseFile(IDbContextFactory<SammlarrDbContext> contexts, ILogger<DatabaseFile> logger) : IDatabaseFile
{
	private const double VacuumWhenFree = 0.1;

	public async Task<Compaction> CompactAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await ctx.Database.OpenConnectionAsync(cancellationToken);
		var connection = ctx.Database.GetDbConnection();
		var before = OnDisk(connection);
		await ExecuteAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken);
		var free = await PragmaAsync(connection, "freelist_count", cancellationToken);
		var pages = await PragmaAsync(connection, "page_count", cancellationToken);
		var vacuumed = false;
		if (free >= pages * VacuumWhenFree)
		{
			vacuumed = await VacuumAsync(connection, cancellationToken);
		}

		await ExecuteAsync(connection, "PRAGMA optimize=0x10002", cancellationToken);
		await ExecuteAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken);
		var after = OnDisk(connection);
		logger.LogInformation("Compacted the database file from {Before} to {After} bytes on disk (vacuumed: {Vacuumed})", before, after, vacuumed);
		return new Compaction(before, after, vacuumed);
	}

	private async Task<bool> VacuumAsync(DbConnection connection, CancellationToken cancellationToken)
	{
		try
		{
			await ExecuteAsync(connection, "VACUUM", cancellationToken);
			return true;
		}
		catch (SqliteException exception)
		{
			logger.LogWarning(exception, "The database file could not be vacuumed, so it keeps its free pages until the next compaction");
			return false;
		}
	}

	private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<long> PragmaAsync(DbConnection connection, string pragma, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = $"PRAGMA {pragma}";
		return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
	}

	private static long OnDisk(DbConnection connection)
	{
		return Length(connection.DataSource) + Length($"{connection.DataSource}-wal");
	}

	private static long Length(string path)
	{
		var file = new FileInfo(path);
		return file.Exists ? file.Length : 0;
	}
}
