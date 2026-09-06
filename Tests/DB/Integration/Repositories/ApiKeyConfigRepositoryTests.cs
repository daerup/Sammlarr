using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using DB.Data.Access;
using Domain.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ApiKeyConfigRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private Mock<ITypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>> _cache = null!;
	private Mock<IMediaRepository> _mediaRepository = null!;
	private IApiKeyConfigRepository _testee = null!;

	private (Guid, ApiKeyProvider) Key(ApiKeyProvider provider) => (fixture.CurrentUser.Id.Value, provider);

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>.Mock();
		_mediaRepository = IMediaRepository.Mock();
		_testee = new ApiKeyConfigRepository(fixture.ContextFactory, _cache.Object, _mediaRepository.Object, new TestApiKeyFactory(), fixture.CurrentUser);
	}

	[Test]
	public async Task SetApiKey_WithEncryption_StoresEncryptedInDatabase()
	{
		var provider = ApiKeyProvider.Tmdb;
		var plainKey = "secret-api-key";

		await _testee.SetApiKeyAsync(new TestApiKey(provider, plainKey));

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
	public async Task SetApiKey_WithVariousKeys_PreservesPlaintext(string apiKey)
	{
		var provider = ApiKeyProvider.Tmdb;

		await _testee.SetApiKeyAsync(new TestApiKey(provider, apiKey));

		var setting = await fixture.Context.ApiKeySettings.SingleAsync();

		await Assert.That(setting.ApiKey).IsEqualTo(apiKey);
	}

	[Test]
	public async Task SetMultipleApiKeys_WithDifferentProviders_StoresIndependently()
	{
		var key = "shared-api-key";

		await _testee.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, key));
		await _testee.SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Mdblist, key));

		var keys = await fixture.Context.ApiKeySettings.ToListAsync();
		var tmdb = keys.Single(s => s.Provider == ApiKeyProvider.Tmdb);
		var imdb = keys.Single(s => s.Provider == ApiKeyProvider.Mdblist);

		await Assert.That(keys).Count().IsEqualTo(2);
		await Assert.That(tmdb.ApiKey).IsEqualTo(key);
		await Assert.That(imdb.ApiKey).IsEqualTo(key);
	}

	[Test]
	public async Task GetApiKeyAsync_ReadsFromCache()
	{
		var provider = ApiKeyProvider.Tmdb;

		await _testee.GetApiKeyAsync(provider);

		_cache.TryGetValue(Key(provider)).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetApiKeyAsync_WhenNotStored_ReturnsError()
	{
		var apiKey = await _testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(apiKey).IsError();
	}

	[Test]
	public async Task GetApiKeyAsync_WhenStored_ReturnsApiKey()
	{
		var provider = ApiKeyProvider.Tmdb;
		await SetUpApiKeyAsync(provider, "secret-api-key");

		var apiKey = await _testee.GetApiKeyAsync(provider);

		await Assert.That(apiKey).IsSuccess().And.Multiple(async resolved => await Assert.That(resolved.Value).IsEqualTo("secret-api-key"));
	}

	[Test]
	public async Task SetApiKey_AddsToCache()
	{
		var provider = ApiKeyProvider.Tmdb;
		var apiKey = new TestApiKey(provider, "secret-api-key");

		await _testee.SetApiKeyAsync(apiKey);

		_cache.Set(Key(provider), apiKey, Any()).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	[Test]
	public async Task Invalidate_RemovesFromCacheOnly()
	{
		var provider = ApiKeyProvider.Tmdb;
		await SetUpApiKeyAsync(provider, "secret-api-key");
		_cache.Reset();

		_testee.Invalidate(provider);

		_cache.Remove(Key(provider)).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
		await Assert.That(await fixture.Context.ApiKeySettings.AnyAsync()).IsTrue();
	}

	[Test]
	public async Task ClearApiKey_RemovesFromCache()
	{
		var provider = ApiKeyProvider.Tmdb;
		await SetUpApiKeyAsync(provider, "secret-api-key");
		_cache.Reset();

		await _testee.RemoveApiKeyAsync(provider);

		_cache.Remove(Key(provider)).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
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

file sealed record TestApiKey(ApiKeyProvider Provider, string Value) : IApiKey;

file sealed class TestApiKeyFactory : IApiKeyFactory
{
	public Task<Result<IApiKey>> CreateAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default) =>
		Task.FromResult<Result<IApiKey>>(Success.Of<IApiKey>(new TestApiKey(provider, value)));
}
