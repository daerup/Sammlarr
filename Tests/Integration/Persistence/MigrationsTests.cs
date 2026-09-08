using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MigrationsTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task Model_has_no_pending_changes_beyond_the_latest_migration()
	{
		await Assert.That(fixture.Context.Database.HasPendingModelChanges()).IsFalse();
	}
}
