using System.Data.Common;
using Contracts.Housekeeping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DB.Data.Housekeeping;

internal sealed class DatabaseFile(IDbContextFactory<SammlarrDbContext> contexts, ILogger<DatabaseFile> logger) : IDatabaseFile
{
	private const double VacuumWhenFree = 0.1;

	private const long BytesPerImdbRating = 13;

	private const long BytesPerAnimeId = 84;

	private const long BytesPerRun = 357;

	private const long BytesPerRunTitle = 359;

	private const long BytesPerRunCalls = 426;

	private const long BytesPerRunTimes = 428;

	private const long BytesPerAbsence = 293;

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

	public async Task<DatabaseSize> SizeAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await ctx.Database.OpenConnectionAsync(cancellationToken);
		var connection = ctx.Database.GetDbConnection();
		var pages = await PragmaAsync(connection, "page_count", cancellationToken);
		var free = await PragmaAsync(connection, "freelist_count", cancellationToken);
		var pageSize = await PragmaAsync(connection, "page_size", cancellationToken);
		return new DatabaseSize(OnDisk(connection), (pages - free) * pageSize);
	}

	public async Task<DatabaseParts> PartsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var history =
			await ctx.FeedRuns.IgnoreQueryFilters().LongCountAsync(cancellationToken) * BytesPerRun
			+ await ctx.FeedRunItems.IgnoreQueryFilters().LongCountAsync(cancellationToken) * BytesPerRunTitle
			+ await ctx.FeedRunCalls.IgnoreQueryFilters().LongCountAsync(cancellationToken) * BytesPerRunCalls
			+ await ctx.FeedRunTimes.IgnoreQueryFilters().LongCountAsync(cancellationToken) * BytesPerRunTimes
			+ await ctx.FeedAbsences.IgnoreQueryFilters().LongCountAsync(cancellationToken) * BytesPerAbsence;
		return new DatabaseParts(
			await ctx.ImdbRatings.LongCountAsync(cancellationToken) * BytesPerImdbRating,
			await ctx.AnimeIds.LongCountAsync(cancellationToken) * BytesPerAnimeId,
			history
		);
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
