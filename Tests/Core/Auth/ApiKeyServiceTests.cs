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
	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IApiKeyConfigRepository> _keys = IApiKeyConfigRepository.Mock();
	private readonly Mock<IApiKeyValidator> _tmdb = IApiKeyValidator.Mock();

	private readonly ApiKeyService Testee;

	public ApiKeyServiceTests()
	{
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("stored-key"));
		_tmdb.CheckAsync(Any(), Any()).Returns(AccessCheck.Valid);
		Testee = new ApiKeyService(
			_keys.Object,
			new FakeKeyedServiceIndex<ApiKeyProvider, IApiKeyValidator>().Register(ApiKeyProvider.Tmdb, _tmdb.Object),
			IFeedService.Mock().Object,
			new SecretCache<ApiKeyProvider, IApiKey>(
				new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory),
				new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), AccessCheck>(_memory),
				new TestCurrentUser()
			)
		);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsAndChecksTheStoredKeyOnce()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess();
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Once);
		_tmdb.CheckAsync("stored-key", Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenTmdbCannotBeReached_StillServesTheStoredKey()
	{
		_tmdb.CheckAsync(Any(), Any()).Returns(AccessCheck.Unreachable);

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess().And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("stored-key"));
	}

	[Test]
	public async Task IsStoredAsync_WhenAKeyIsStored_SaysSoWithoutCheckingIt()
	{
		_keys.GetAsync(ApiKeyProvider.Mdblist, Any()).Returns(Error.From("No API key is configured for this provider."));

		await Assert.That(await Testee.IsStoredAsync(ApiKeyProvider.Tmdb)).IsTrue();
		await Assert.That(await Testee.IsStoredAsync(ApiKeyProvider.Mdblist)).IsFalse();
		_tmdb.CheckAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ReportRejected_AfterARead_FailsTheNextReadWithoutAskingAgain()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		Testee.ReportRejected(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsError();
		_tmdb.CheckAsync(Any(), Any()).WasCalled(Times.Once);
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
	public async Task StoreAsync_AfterARejection_ServesTheNewKey()
	{
		Testee.ReportRejected(ApiKeyProvider.Tmdb);

		await Testee.StoreAsync(new TestApiKey(ApiKeyProvider.Tmdb, "new-key"));

		await Assert.That(await Testee.GetAsync(ApiKeyProvider.Tmdb)).IsSuccess();
	}

	[Test]
	public async Task ClearAsync_AfterARead_DropsTheCachedKey()
	{
		await Testee.GetAsync(ApiKeyProvider.Tmdb);

		await Testee.ClearAsync(ApiKeyProvider.Tmdb);

		await Testee.GetAsync(ApiKeyProvider.Tmdb);
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task TestAsync_ForAKeyTmdbAccepts_IsThatKey()
	{
		await Assert
			.That(await Testee.TestAsync(ApiKeyProvider.Tmdb, "typed-key"))
			.IsSuccess()
			.And.Multiple(async key => await Assert.That(key.Value).IsEqualTo("typed-key"));
	}

	[Test]
	[Arguments(AccessCheck.Rejected, "The TMDB API key is not valid.")]
	[Arguments(AccessCheck.Unreachable, "TMDB could not be reached to check the API key. Try again in a moment.")]
	[Arguments(AccessCheck.Spent, "TMDB is limiting requests right now. Try the API key again in a moment.")]
	public async Task TestAsync_ForAKeyTmdbDoesNotAccept_SaysWhy(AccessCheck answer, string reason)
	{
		_tmdb.CheckAsync("typed-key", Any()).Returns(answer);

		await Assert
			.That(await Testee.TestAsync(ApiKeyProvider.Tmdb, "typed-key"))
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo(reason));
	}
}
