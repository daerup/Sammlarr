using Contracts.Auth;
using Contracts.Settings;
using Core.Auth;
using Core.Caching;
using Core.Providers;
using Domain.Auth;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Providers;

public sealed class ServiceReadinessTests : IDisposable
{
	private static readonly ArrInstanceConfig Reachable = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878"), ApiKey = Some.Of("arr-key") };

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<IApiKeyConfigRepository> _keys = IApiKeyConfigRepository.Mock();
	private readonly Mock<ICredentialRepository> _credentials = ICredentialRepository.Mock();
	private readonly Mock<IArrSinkConfigService> _arr = IArrSinkConfigService.Mock();
	private readonly SecretCache<ApiKeyProvider, IApiKey> _keyChecks;
	private readonly SecretCache<AuthProvider, ICredential> _signInChecks;
	private readonly ServiceReadiness Testee;

	public ServiceReadinessTests()
	{
		var user = new TestCurrentUser();
		_keyChecks = new SecretCache<ApiKeyProvider, IApiKey>(
			new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory),
			new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), AccessCheck>(_memory),
			user
		);
		_signInChecks = new SecretCache<AuthProvider, ICredential>(
			new TypedMemoryCache<(Guid User, AuthProvider Provider), ICredential>(_memory),
			new TypedMemoryCache<(Guid User, AuthProvider Provider), AccessCheck>(_memory),
			user
		);
		_keys.GetAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No key is stored."));
		_credentials.GetAsync(Any<AuthProvider>(), Any()).Returns(Error.From("No sign-in is stored."));
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		Testee = new ServiceReadiness(_keys.Object, _credentials.Object, _arr.Object, _keyChecks, _signInChecks);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task ConnectedAsync_ForAServiceAnyoneMayCall_IsConnected()
	{
		await Assert.That(await Testee.ConnectedAsync([RemoteService.Wikidata])).IsEquivalentTo([RemoteService.Wikidata]);
	}

	[Test]
	public async Task ConnectedAsync_ForAStoredKey_IsConnected()
	{
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("tmdb-key"));

		await Assert.That(await Testee.ConnectedAsync([RemoteService.Tmdb, RemoteService.Mdblist])).IsEquivalentTo([RemoteService.Tmdb]);
	}

	[Test]
	public async Task ConnectedAsync_ForAKeyTheServiceRejected_IsNotConnected()
	{
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("tmdb-key"));

		_keyChecks.Reject(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.ConnectedAsync([RemoteService.Tmdb])).IsEmpty();
	}

	[Test]
	[Arguments(AccessCheck.Unreachable)]
	[Arguments(AccessCheck.Spent)]
	public async Task ConnectedAsync_ForAKeyItCouldNotCheck_StaysConnected(AccessCheck answer)
	{
		_keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of("tmdb-key"));

		await _keyChecks.GetOrLoadAsync(
			ApiKeyProvider.Tmdb,
			() => Task.FromResult<Result<IApiKey>>(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "tmdb-key"))),
			_ => Task.FromResult(answer)
		);

		await Assert.That(await Testee.ConnectedAsync([RemoteService.Tmdb])).IsEquivalentTo([RemoteService.Tmdb]);
	}

	[Test]
	public async Task ConnectedAsync_ForAStoredSignIn_IsConnectedUntilItIsRejected()
	{
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(new StoredCredential("plex-token", DateTimeOffset.MaxValue, Guid.NewGuid())));

		await Assert.That(await Testee.ConnectedAsync([RemoteService.Plex])).IsEquivalentTo([RemoteService.Plex]);
		_signInChecks.Reject(AuthProvider.Plex);
		await Assert.That(await Testee.ConnectedAsync([RemoteService.Plex])).IsEmpty();
	}

	[Test]
	public async Task ConnectedAsync_ForAnAppTheInstallReaches_IsConnected()
	{
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = Reachable });

		await Assert.That(await Testee.ConnectedAsync([RemoteService.Radarr, RemoteService.Sonarr])).IsEquivalentTo([RemoteService.Radarr]);
	}

	[Test]
	public async Task ReadyAsync_ForRadarrAndSonarr_NeedsBothApps()
	{
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = Reachable });

		await Assert.That(await Testee.ReadyAsync(new Domain.Providers.Arr())).IsFalse();
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = Reachable, Sonarr = Reachable });
		await Assert.That(await Testee.ReadyAsync(new Domain.Providers.Arr())).IsTrue();
	}

	[Test]
	public async Task ReadyAsync_ForSammlarrAccounts_IsAlwaysReady()
	{
		await Assert.That(await Testee.ReadyAsync(new SammlarrAccounts())).IsTrue();
	}
}
