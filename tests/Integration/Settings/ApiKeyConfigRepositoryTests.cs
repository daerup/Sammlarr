using Contracts.Auth;
using DB.Data.Auth;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ApiKeyConfigRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private IApiKeyConfigRepository Testee => new ApiKeyConfigRepository(fixture.ContextFactory);

	[Test]
	public async Task StoreAsync_WithDifferentProviders_StoresIndependently()
	{
		var key = "shared-api-key";

		await Testee.StoreAsync(ApiKeyProvider.Tmdb, new Secret(key));
		await Testee.StoreAsync(ApiKeyProvider.Mdblist, new Secret(key));

		var keys = await fixture.Context.ApiKeySettings.ToListAsync();
		var tmdb = keys.Single(s => s.Provider == ApiKeyProvider.Tmdb);
		var mdblist = keys.Single(s => s.Provider == ApiKeyProvider.Mdblist);

		await Assert.That(keys).Count().IsEqualTo(2);
		await Assert.That(tmdb.ApiKey.Reveal()).IsEqualTo(key);
		await Assert.That(mdblist.ApiKey.Reveal()).IsEqualTo(key);
	}

	[Test]
	public async Task GetAsync_WhenNotStored_ReturnsError()
	{
		var apiKey = await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Assert.That(apiKey).IsError();
	}

	[Test]
	public async Task GetAsync_WhenStored_ReturnsTheStoredValue()
	{
		await SetUpApiKeyAsync(ApiKeyProvider.Tmdb, "secret-api-key");

		var apiKey = await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Assert.That(apiKey).IsSuccess().And.Multiple(async value => await Assert.That(value.Reveal()).IsEqualTo("secret-api-key"));
	}

	[Test]
	public async Task ClearAsync_WhenStored_DeletesIt()
	{
		await SetUpApiKeyAsync(ApiKeyProvider.Tmdb, "secret-api-key");

		await Testee.ClearAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await fixture.Context.ApiKeySettings.AnyAsync()).IsFalse();
	}

	private async Task SetUpApiKeyAsync(ApiKeyProvider provider, string apiKey)
	{
		await fixture.Context.ApiKeySettings.AddAsync(new ApiKeyConfigRow { Provider = provider, ApiKey = new Secret(apiKey) });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
	}
}
