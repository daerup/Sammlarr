using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using DB.Data.Access;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class ApiKeyConfigRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private Mock<ITypedMemoryCache<MediaProvider, string>> _cache = null!;
	private Mock<IMediaRepository> _mediaRepository = null!;
	private IApiKeyConfigRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<MediaProvider, string>.Mock();
		_mediaRepository = IMediaRepository.Mock();
		_testee = new ApiKeyConfigRepository(fixture.ContextFactory, _cache.Object, _mediaRepository.Object);
	}

	[Test]
	public async Task SetApiKey_WithEncryption_StoresEncryptedInDatabase()
	{
		var provider = MediaProvider.Tmdb;
		var plainKey = "secret-api-key";

		await _testee.SetApiKeyAsync(provider, plainKey);

		var encrypted = (await fixture.RawContext.Table<ApiKeyConfigEntity>().QueryAsync(x => x.ApiKey)).Single();

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

		await _testee.SetApiKeyAsync(provider, apiKey);

		var setting = await fixture.Context.ApiKeySettings.SingleAsync();

		await Assert.That(setting.ApiKey).IsEqualTo(apiKey);
	}

	[Test]
	public async Task SetMultipleApiKeys_WithDifferentProviders_StoresIndependently()
	{
		var key = "shared-api-key";

		await _testee.SetApiKeyAsync(MediaProvider.Tmdb, key);
		await _testee.SetApiKeyAsync(MediaProvider.Imdb, key);

		var keys = await fixture.Context.ApiKeySettings.ToListAsync();
		var tmdb = keys.Single(s => s.Provider == MediaProvider.Tmdb);
		var imdb = keys.Single(s => s.Provider == MediaProvider.Imdb);

		await Assert.That(keys).Count().IsEqualTo(2);
		await Assert.That(tmdb.ApiKey).IsEqualTo(key);
		await Assert.That(imdb.ApiKey).IsEqualTo(key);
	}

	[Test]
	public async Task GetApiKeyAsync_ReadsFromCache()
	{
		var provider = MediaProvider.Tmdb;

		await _testee.GetApiKeyAsync(provider);

		_cache.TryGetValue(provider).WasCalled(Times.Once);
	}

	[Test]
	public async Task SetApiKey_AddsToCache()
	{
		var provider = MediaProvider.Tmdb;
		var apiKey = "secret-api-key";

		await _testee.SetApiKeyAsync(provider, apiKey);

		_cache.Set(provider, apiKey).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	[Test]
	public async Task ClearApiKey_RemovesFromCache()
	{
		var provider = MediaProvider.Tmdb;
		await SetUpApiKeyAsync(provider, "secret-api-key");
		_cache.Reset();

		await _testee.RemoveApiKeyAsync(provider);

		_cache.Remove(provider).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	private async Task SetUpApiKeyAsync(MediaProvider provider, string apiKey)
	{
		await fixture.Context.ApiKeySettings.AddAsync(new ApiKeyConfigEntity { Provider = provider, ApiKey = apiKey });
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
