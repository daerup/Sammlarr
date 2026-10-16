using Harness;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.DB.Migrations;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AccountsOnIdentityMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Before = "20261005194202_ArrTagProtection";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Migrate_PastAccountsOnIdentity_KeepsTheUsersAndTheirFeeds()
	{
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		await using var context = host.ContextOn(connection);
		var migrator = context.GetService<IMigrator>();
		await RawSql.ExecuteAsync(connection, migrator.GenerateScript(toMigration: Before));
		var user = Guid.NewGuid();
		await RawSql.ExecuteAsync(connection, "PRAGMA foreign_keys = ON;");
		await RawSql.ExecuteAsync(connection, RawSql.PlexUser(user, "Ada"));
		await RawSql.ExecuteAsync(connection, RawSql.Feed(Guid.NewGuid(), user, "Mdblist"));

		await migrator.MigrateAsync();

		await Assert.That(await RawSql.ReadAsync(connection, "SELECT DisplayName FROM AspNetUsers WHERE JoinedAt IS NOT NULL")).IsEquivalentTo(["Ada"]);
		await Assert.That(await RawSql.ReadAsync(connection, "SELECT UserId FROM Feeds")).IsEquivalentTo([$"{user}"]);
	}
}
