using Contracts.Auth;
using DB.Data.Access;
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
	public async Task StoreAsync_WithEncryption_StoresEncryptedInDatabase()
	{
		var plainKey = "secret-api-key";

		await Testee.StoreAsync(ApiKeyProvider.Tmdb, plainKey);

		var encrypted = (await fixture.RawContext.Table<ApiKeyConfigRow>().QueryAsync(x => x.ApiKey)).Single();

		await Assert.That(encrypted).IsNotEqualTo(plainKey);
		await Assert.That(encrypted).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	[Arguments("simple-key")]
	[Arguments("key-with-dashes-123")]
	[Arguments("special!@#$%^&*()")]
	[Arguments("unicode-café")]
	[Arguments("")]
	public async Task StoreAsync_WithVariousKeys_PreservesPlaintext(string apiKey)
	{
		await Testee.StoreAsync(ApiKeyProvider.Tmdb, apiKey);

		var setting = await fixture.Context.ApiKeySettings.SingleAsync();

		await Assert.That(setting.ApiKey).IsEqualTo(apiKey);
	}

	[Test]
	public async Task StoreAsync_WithDifferentProviders_StoresIndependently()
	{
		var key = "shared-api-key";

		await Testee.StoreAsync(ApiKeyProvider.Tmdb, key);
		await Testee.StoreAsync(ApiKeyProvider.Mdblist, key);

		var keys = await fixture.Context.ApiKeySettings.ToListAsync();
		var tmdb = keys.Single(s => s.Provider == ApiKeyProvider.Tmdb);
		var mdblist = keys.Single(s => s.Provider == ApiKeyProvider.Mdblist);

		await Assert.That(keys).Count().IsEqualTo(2);
		await Assert.That(tmdb.ApiKey).IsEqualTo(key);
		await Assert.That(mdblist.ApiKey).IsEqualTo(key);
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

		await Assert.That(apiKey).IsSuccess().And.Multiple(async value => await Assert.That(value).IsEqualTo("secret-api-key"));
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
		await fixture.Context.ApiKeySettings.AddAsync(new ApiKeyConfigRow { Provider = provider, ApiKey = apiKey });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
	}

	private static bool IsBase64OrEmpty(string? value)
	{
		if (value is null)
		{
			return false;
		}
		if (string.IsNullOrEmpty(value))
		{
			return true;
		}

		var buffer = new Span<byte>(new byte[value.Length]);
		return Convert.TryFromBase64String(value, buffer, out _);
	}
}
