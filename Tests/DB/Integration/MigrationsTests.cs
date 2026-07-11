using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MigrationsTests(SQLiteFixture fixture)
{
	[Test]
	public async Task Model_has_no_pending_changes_beyond_the_latest_migration()
	{
		await Assert.That(fixture.Context.Database.HasPendingModelChanges()).IsFalse();
	}
}
