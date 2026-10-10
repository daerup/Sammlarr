using Harness;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.DB.Migrations;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DropTmdbFeedsMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Before = "20261005194202_ArrTagProtection";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Migrate_PastDropTmdbFeeds_DeletesTheTmdbFeedsAndTheirSchedulesOnly()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		await using var context = host.ContextOn(connection);
		var migrator = context.GetService<IMigrator>();
		await ExecuteAsync(connection, migrator.GenerateScript(toMigration: Before));
		var tmdb = Guid.NewGuid();
		var mdblist = Guid.NewGuid();
		await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;");
		await ExecuteAsync(connection, Feed(tmdb, "Tmdb"));
		await ExecuteAsync(connection, Feed(mdblist, "Mdblist"));
		await ExecuteAsync(connection, Schedule(tmdb));
		await ExecuteAsync(connection, Schedule(mdblist));

		await migrator.MigrateAsync();

		await Assert.That(await ReadAsync(connection, "SELECT Provider FROM Feeds")).IsEquivalentTo(["Mdblist"]);
		await Assert.That(await ReadAsync(connection, "SELECT Id FROM CronTickers")).IsEquivalentTo([$"{mdblist}"]);
	}

	private static string Feed(Guid id, string provider)
	{
		return $"""
			INSERT INTO Feeds (Id, CreatedAt, UpdatedAt, UserId, Identifier, IsEnabled, Name, Provider, Strategy, "Trigger")
			VALUES ('{id}', 0, 0, '{Guid.NewGuid()}', 'list', 1, '{provider} list', '{provider}', '"append"', '"manual"');
			""";
	}

	private static string Schedule(Guid id)
	{
		return $"""
			INSERT INTO CronTickers (Id, Retries, IsEnabled, IsSystemPaused, CreatedAt, UpdatedAt)
			VALUES ('{id}', 0, 1, 0, '2026-10-06 00:00:00', '2026-10-06 00:00:00');
			""";
	}

	private static async Task ExecuteAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<IReadOnlyList<string>> ReadAsync(SqliteConnection connection, string sql)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		await using var reader = await command.ExecuteReaderAsync();
		var values = new List<string>();
		while (await reader.ReadAsync())
		{
			values.Add(reader.GetString(0));
		}

		return values;
	}
}
