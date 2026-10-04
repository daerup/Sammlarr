using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SearchDefaultMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeSearchDefault = "20261004043312_PersonHues";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Applying_OnAFreshInstall_SearchesOnTmdb()
	{
		await MigrateAsync(BeforeSearchDefault);
		await host.NoUsers();

		await MigrateAsync(null);

		await Assert.That(await SearchAsync()).IsEqualTo(SearchProvider.Tmdb);
	}

	[Test]
	public async Task Applying_OnAnInstallWithPeople_KeepsItsSearch()
	{
		await MigrateAsync(BeforeSearchDefault);

		await MigrateAsync(null);

		await Assert.That(await SearchAsync()).IsEqualTo(SearchProvider.Plex);
	}

	private async Task<SearchProvider> SearchAsync()
	{
		var config = await host.Context.ProviderSettings.AsNoTracking().SingleAsync();
		return config.SearchProvider;
	}

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);
}
