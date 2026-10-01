using Core.Auth;
using Core.Caching;
using Domain.Auth;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Auth;

public sealed class SecretCacheTests : IDisposable
{
	private static readonly IApiKey Stored = new TestApiKey(ApiKeyProvider.Tmdb, "stored-key");

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly TestCurrentUser _user = new();
	private readonly SecretCache<ApiKeyProvider, IApiKey> Testee;
	private int _loads;

	public SecretCacheTests() =>
		Testee = new SecretCache<ApiKeyProvider, IApiKey>(new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory), _user);

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetOrLoadAsync_OnAMiss_LoadsOnceAndServesTheNextReadFromTheCache()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load)).IsSuccess().And.IsEqualTo(Stored);
		await Assert.That(_loads).IsEqualTo(1);
	}

	[Test]
	public async Task GetOrLoadAsync_AfterAPut_ServesThePutSecretWithoutLoading()
	{
		Testee.Put(ApiKeyProvider.Tmdb, Stored);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load)).IsSuccess().And.IsEqualTo(Stored);
		await Assert.That(_loads).IsEqualTo(0);
	}

	[Test]
	public async Task GetOrLoadAsync_AfterAnEvict_LoadsAgain()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);
		Testee.Evict(ApiKeyProvider.Tmdb);

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_ForAnotherUser_NeverServesTheFirstUsersSecret()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);
		_user.Id = UserId.From(Guid.NewGuid());

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_WhenSignedOut_NeverServesTheSignedInUsersSecret()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);
		_user.IsAuthenticated = false;

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_WhenTheLoadFails_CachesNothing()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Fail);

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load);

		await Assert.That(_loads).IsEqualTo(2);
	}

	private Task<Result<IApiKey>> Load()
	{
		_loads++;
		return Task.FromResult<Result<IApiKey>>(Success.Of(Stored));
	}

	private Task<Result<IApiKey>> Fail()
	{
		_loads++;
		return Task.FromResult<Result<IApiKey>>(Error.From("The key was rejected."));
	}
}
