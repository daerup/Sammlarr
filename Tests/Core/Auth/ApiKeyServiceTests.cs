using Contracts.Auth;
using Contracts.Feed;
using Core.Auth;
using Core.Caching;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Auth;

public sealed class ApiKeyServiceTests : IDisposable
{
	private static readonly IApiKey StoredKey = new TestApiKey(ApiKeyProvider.Tmdb, "stored-key");

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IApiKeyConfigRepository> _keys = IApiKeyConfigRepository.Mock();
	private readonly Mock<IApiKeyFactory> _factory = IApiKeyFactory.Mock();

	private readonly ApiKeyService Testee;

	public ApiKeyServiceTests()
	{
		_keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("stored-key"));
		_factory.CreateAsync(ApiKeyProvider.Tmdb, "stored-key", Any()).Returns(Success.Of(StoredKey));
		Testee = new ApiKeyService(
			_keys.Object,
			_factory.Object,
			IFeedService.Mock().Object,
			new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory),
			new TestCurrentUser()
		);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetApiKeyAsync_WhenReadTwice_LoadsAndValidatesTheStoredKeyOnce()
	{
		await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsSuccess();
		_keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Once);
		_factory.CreateAsync(ApiKeyProvider.Tmdb, "stored-key", Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Invalidate_AfterARead_MakesTheNextReadLoadTheStoredKeyAgain()
	{
		await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);

		Testee.Invalidate(ApiKeyProvider.Tmdb);

		await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);
		_keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task SaveApiKeyAsync_ServesTheSavedKeyWithoutReadingItBack()
	{
		var saved = new TestApiKey(ApiKeyProvider.Tmdb, "new-key");

		await Testee.SaveApiKeyAsync(saved);

		await Assert.That(await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb)).IsSuccess().And.Multiple(async key => await Assert.That(key).IsEqualTo(saved));
		_keys.SetApiKeyAsync(ApiKeyProvider.Tmdb, "new-key", Any()).WasCalled(Times.Once);
		_keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task RemoveApiKeyAsync_AfterARead_DropsTheCachedKey()
	{
		await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);

		await Testee.RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

		await Testee.GetApiKeyAsync(ApiKeyProvider.Tmdb);
		_keys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Exactly(2));
	}
}
