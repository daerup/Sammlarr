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
		await RawSql.ExecuteAsync(connection, migrator.GenerateScript(toMigration: Before));
		var owner = Guid.NewGuid();
		var tmdb = Guid.NewGuid();
		var mdblist = Guid.NewGuid();
		await RawSql.ExecuteAsync(connection, RawSql.PlexUser(owner, "Ada"));
		await RawSql.ExecuteAsync(connection, RawSql.Feed(tmdb, owner, "Tmdb"));
		await RawSql.ExecuteAsync(connection, RawSql.Feed(mdblist, owner, "Mdblist"));
		await RawSql.ExecuteAsync(connection, Schedule(tmdb));
		await RawSql.ExecuteAsync(connection, Schedule(mdblist));

		await migrator.MigrateAsync();

		await Assert.That(await RawSql.ReadAsync(connection, "SELECT Provider FROM Feeds")).IsEquivalentTo(["Mdblist"]);
		await Assert.That(await RawSql.ReadAsync(connection, "SELECT Id FROM CronTickers")).IsEquivalentTo([$"{mdblist}"]);
	}

	private static string Schedule(Guid id)
	{
		return $"""
			INSERT INTO CronTickers (Id, Retries, IsEnabled, IsSystemPaused, CreatedAt, UpdatedAt)
			VALUES ('{id}', 0, 1, 0, '2026-10-06 00:00:00', '2026-10-06 00:00:00');
			""";
	}
}
