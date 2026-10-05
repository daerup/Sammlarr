using DB.Observability;

namespace DB.Tests.Observability;

public class SqlSummaryTests
{
	[Test]
	[Arguments("""SELECT "m"."Id", "m"."Name" FROM "Media" AS "m" WHERE "m"."Id" = @id""", "SELECT", "Media")]
	[Arguments("""INSERT INTO "ExternalMediaIds" ("Id", "MediaId") VALUES (@p0, @p1)""", "INSERT", "ExternalMediaIds")]
	[Arguments("""UPDATE "Wantlists" SET "UpdatedAt" = @p0 WHERE "Id" = @id""", "UPDATE", "Wantlists")]
	[Arguments("""DELETE FROM "Blocklist" WHERE "Id" = @id""", "DELETE", "Blocklist")]
	[Arguments("select id from Configs where Key = @key", "SELECT", "Configs")]
	public async Task Parse_ExtractsOperationAndTable(string sql, string expectedOperation, string expectedTable)
	{
		var (operation, table) = SqlSummary.Parse(sql);

		await Assert.That(operation).IsEqualTo(expectedOperation);
		await Assert.That(table).IsEqualTo(expectedTable);
	}

	[Test]
	public async Task Parse_UnknownCommand_ReturnsNulls()
	{
		var (operation, table) = SqlSummary.Parse("PRAGMA journal_mode=WAL;");

		await Assert.That(operation).IsNull();
		await Assert.That(table).IsNull();
	}
}
