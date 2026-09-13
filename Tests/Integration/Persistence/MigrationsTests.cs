using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MigrationsTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task Model_WhenComparedToTheLatestMigration_HasNoPendingChanges()
	{
		await Assert.That(fixture.Context.Database.HasPendingModelChanges()).IsFalse();
	}
}
