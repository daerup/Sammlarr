using Contracts.Auth;
using Contracts.Media;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DB.Tests.Integration;

public class ApiKeyStoreTests : IAsyncDisposable
{
	private readonly EncryptionTestFixture _fixture = new();
	private SammlarrDbContext Context => field ??= _fixture.CreateDbContext();
	private RawDbContext RawContext => field ??= new RawDbContext(Context);
	private IApiKeyStore Store => field ??= new ApiKeyStore(Context, new MemoryCache(new MemoryCacheOptions()));

	public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

	[Test]
	public async Task GetApiKey_WhenNotSet_ReturnsNull()
	{
		var apiKey = await Store.GetApiKeyAsync(MediaProvider.Tmdb);

		await Assert.That(apiKey).IsNull();
	}

	[Test]
	public async Task SetApiKey_StoresAndRetrievesSuccessfully()
	{
		var provider = MediaProvider.Tmdb;
		var plainKey = "tmdb-api-key-12345";

		await Store.SetApiKeyAsync(provider, plainKey);

		var retrieved = await Store.GetApiKeyAsync(provider);

		await Assert.That(retrieved).IsEqualTo(plainKey);
	}

	[Test]
	public async Task SetApiKey_WithEncryption_StoresEncryptedInDatabase()
	{
		var provider = MediaProvider.Tmdb;
		var plainKey = "secret-api-key";

		await Store.SetApiKeyAsync(provider, plainKey);

		var encrypted = (await RawContext.Table<ApiKeySetting>().QueryAsync(x => x.ApiKey)).Single();

		await Assert.That(encrypted).IsNotEqualTo(plainKey);
		await Assert.That(encrypted).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	[Arguments("simple-key")]
	[Arguments("key-with-dashes-123")]
	[Arguments("special!@#$%^&*()")]
	[Arguments("unicode-café")]
	[Arguments("")]
	public async Task SetApiKey_WithVariousKeys_PreservesPlaintext(string apiKey)
	{
		var provider = MediaProvider.Tmdb;

		await Store.SetApiKeyAsync(provider, apiKey);

		var retrieved = await Store.GetApiKeyAsync(provider);

		await Assert.That(retrieved).IsEqualTo(apiKey);
	}

	[Test]
	public async Task UpdateApiKey_WithNewKey_UpdatesValue()
	{
		var provider = MediaProvider.Tmdb;

		var oldKey = "old-key";
		await Store.SetApiKeyAsync(provider, oldKey);

		var newKey = "new-api-key-12345";
		await Store.SetApiKeyAsync(provider, newKey);

		var retrieved = await Store.GetApiKeyAsync(provider);

		await Assert.That(retrieved).IsEqualTo(newKey);
	}

	[Test]
	public async Task SetMultipleApiKeys_WithDifferentProviders_StoresIndependently()
	{
		var key = "shared-api-key";

		await Store.SetApiKeyAsync(MediaProvider.Tmdb, key);
		await Store.SetApiKeyAsync(MediaProvider.Imdb, key);

		var tmdbKey = await Store.GetApiKeyAsync(MediaProvider.Tmdb);
		var imdbKey = await Store.GetApiKeyAsync(MediaProvider.Imdb);

		await Assert.That(tmdbKey).IsEqualTo(key);
		await Assert.That(imdbKey).IsEqualTo(key);
	}

	[Test]
	public async Task ClearApiKey_RemovesStoredData()
	{
		var provider = MediaProvider.Tmdb;
		var apiKey = "secret-api-key";

		await Store.SetApiKeyAsync(provider, apiKey);

		await Store.ClearApiKeyAsync(provider);
		var storedAfter = await Store.GetApiKeyAsync(provider);

		await Assert.That(storedAfter).IsNull();
	}

	private static bool IsBase64OrEmpty(string? value)
	{
		if (value is null)
			return false;
		if (string.IsNullOrEmpty(value))
			return true;

		Span<byte> buffer = new Span<byte>(new byte[value.Length]);
		return Convert.TryFromBase64String(value, buffer, out _);
	}
}
