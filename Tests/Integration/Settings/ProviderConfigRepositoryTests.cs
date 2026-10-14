using Contracts.Settings;
using DB.Data.Config;
using Domain.Providers;
using Domain.Settings;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ProviderConfigRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IProviderConfigRepository Testee => new ProviderConfigRepository(fixture.ContextFactory);

	[Test]
	public async Task GetAsync_OnFreshDatabase_ReturnsSeededDefaults()
	{
		var providerSettings = await Testee.GetAsync();

		await Assert.That(providerSettings).IsNotNull();
		await Assert.That(providerSettings.Sink).IsEqualTo(SinkKind.Plex);
		await Assert.That(providerSettings.TrendingProvider).IsEqualTo(TrendingProvider.Tmdb);
		await Assert.That(providerSettings.AuthProvider).IsEqualTo(AuthProvider.Plex);
	}

	[Test]
	public async Task StoreAsync_WithValuesOtherThanTheSeededOnes_ReadsThemBackFromTheOneRow()
	{
		var stored = new ProviderConfig
		{
			Sink = SinkKind.Arr,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Tmdb,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = RegistrationMode.Closed,
		};

		await Testee.StoreAsync(stored);

		await Assert.That(await Testee.GetAsync()).IsEqualTo(stored);
		await Assert.That(await fixture.Context.ProviderSettings.CountAsync()).IsEqualTo(1);
	}

	[Test]
	public async Task Migrate_WithTheRowMissing_PutsBackOneRowOfDefaults()
	{
		await fixture.Context.ProviderSettings.ExecuteDeleteAsync();

		await fixture.Context.Database.MigrateAsync();

		await Assert.That(await fixture.Context.ProviderSettings.AsNoTracking().ToListAsync()).IsEquivalentTo([ProviderConfigRow.Default]);
	}

	[Test]
	public async Task Migrate_WithTheRowEdited_KeepsTheEdits()
	{
		var edited = await fixture.Context.ProviderSettings.AsNoTracking().SingleAsync();

		await fixture.Context.Database.MigrateAsync();

		await Assert.That(await fixture.Context.ProviderSettings.AsNoTracking().SingleAsync()).IsEqualTo(edited);
		await Assert.That(edited).IsNotEqualTo(ProviderConfigRow.Default);
	}
}
