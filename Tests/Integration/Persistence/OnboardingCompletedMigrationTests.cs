using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class OnboardingCompletedMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeOnboarding = "20261004051349_SearchDefaultsToTmdb";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_OnAnInstallWithPeople_MarksSetupAsDone()
	{
		await MigrateAsync(BeforeOnboarding);

		await MigrateAsync(null);

		await Assert.That(await CompletedAtAsync()).IsNotNull();
	}

	[Test]
	public async Task Applying_OnAFreshInstall_LeavesSetupToDo()
	{
		await MigrateAsync(BeforeOnboarding);
		await host.NoUsers();

		await MigrateAsync(null);

		await Assert.That(await CompletedAtAsync()).IsNull();
	}

	private async Task<DateTimeOffset?> CompletedAtAsync()
	{
		var config = await host.Context.ProviderSettings.AsNoTracking().SingleAsync();
		return config.OnboardingCompletedAt;
	}

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);
}
