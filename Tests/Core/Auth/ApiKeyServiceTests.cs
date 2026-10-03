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
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("stored-key"));
		_factory.CreateAsync(ApiKeyProvider.Tmdb, "stored-key", Any()).Returns(Success.Of(StoredKey));
		Testee = new ApiKeyService(
			_keys.Object,
			_factory.Object,
			IFeedService.Mock().Object,
			new SecretCache<ApiKeyProvider, IApiKey>(new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory), new TestCurrentUser())
		);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsAndValidatesTheStoredKeyOnce()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess();
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Once);
		_factory.CreateAsync(ApiKeyProvider.Tmdb, "stored-key", Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task IsStoredAsync_WhenAKeyIsStored_SaysSoWithoutValidatingIt()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Error.From("No API key is configured for this provider."));

		await Assert.That(await Testee.IsStoredAsync(ApiKeyProvider.Tmdb)).IsTrue();
		await Assert.That(await Testee.IsStoredAsync(ApiKeyProvider.Mdblist)).IsFalse();
		_factory.CreateAsync(Any<ApiKeyProvider>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ReportRejected_AfterARead_MakesTheNextReadLoadTheStoredKeyAgain()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		Testee.ReportRejected(ApiKeyProvider.Tmdb);

		await Testee.GetAsync(ApiKeyProvider.Tmdb);
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task StoreAsync_ServesTheSavedKeyWithoutReadingItBack()
	{
		var saved = new TestApiKey(ApiKeyProvider.Tmdb, "new-key");

		await Testee.StoreAsync(saved);

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess().And.Multiple(async key => await Assert.That(key).IsEqualTo(saved));
		_keys.StoreAsync(ApiKeyProvider.Tmdb, "new-key", Any()).WasCalled(Times.Once);
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ClearAsync_AfterARead_DropsTheCachedKey()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Testee.ClearAsync(ApiKeyProvider.Tmdb);

		await Testee.GetAsync(ApiKeyProvider.Tmdb);
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Exactly(2));
	}
}
