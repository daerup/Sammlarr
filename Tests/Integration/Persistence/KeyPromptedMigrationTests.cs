using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class KeyPromptedMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeKeyPrompt = "20261004061136_OnboardingCompletedAt";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_LeavesEveryoneAlreadyThereUnaskedForAKey()
	{
		await host.Context.GetService<IMigrator>().MigrateAsync(BeforeKeyPrompt);

		await host.Context.GetService<IMigrator>().MigrateAsync();

		var prompted = await host.Context.Users.AsNoTracking().Select(user => user.KeyPromptedAt).ToListAsync();
		await Assert.That(prompted).IsNotEmpty();
		await Assert.That(prompted).All(stamp => stamp != null);
	}
}
