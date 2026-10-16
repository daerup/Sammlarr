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
		Testee = new SecretCache<ApiKeyProvider, IApiKey>(
			new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), IApiKey>(_memory),
			new TypedMemoryCache<(Guid User, ApiKeyProvider Provider), AccessCheck>(_memory),
			_user
		);

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetOrLoadAsync_OnAMiss_LoadsOnceAndServesTheNextReadFromTheCache()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsSuccess().And.IsEqualTo(Stored);
		await Assert.That(_loads).IsEqualTo(1);
	}

	[Test]
	public async Task GetOrLoadAsync_AfterAPut_ServesThePutSecretWithoutLoading()
	{
		Testee.Put(ApiKeyProvider.Tmdb, Stored);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsSuccess().And.IsEqualTo(Stored);
		await Assert.That(_loads).IsEqualTo(0);
	}

	[Test]
	public async Task GetOrLoadAsync_AfterAnEvict_LoadsAgain()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);
		Testee.Evict(ApiKeyProvider.Tmdb);

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_ForAnotherUser_NeverServesTheFirstUsersSecret()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);
		_user.Id = UserId.From(Guid.NewGuid());

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_WhenSignedOut_NeverServesTheSignedInUsersSecret()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);
		_user.IsAuthenticated = false;

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_WhenTheLoadFails_CachesNothing()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Fail, Check);

		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		await Assert.That(_loads).IsEqualTo(2);
	}

	[Test]
	public async Task GetOrLoadAsync_WhenTheCheckRejectsTheSecret_FailsAsRejectedAndRemembersItWithoutLoadingAgain()
	{
		var rejected = await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Answering(AccessCheck.Rejected))).IsError();

		await Assert.That(rejected!.CausedBy<SecretRejectedException>()).IsSome();
		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsError();
		await Assert.That(_loads).IsEqualTo(1);
	}

	[Test]
	[Arguments(AccessCheck.Unreachable)]
	[Arguments(AccessCheck.Spent)]
	public async Task GetOrLoadAsync_WhenTheCheckCannotTell_ServesTheSecretWithoutKeepingIt(AccessCheck answer)
	{
		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Answering(answer))).IsSuccess().And.IsEqualTo(Stored);
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Answering(answer));
		await Assert.That(_loads).IsEqualTo(2);
		await Assert.That(Testee.IsRejected(ApiKeyProvider.Tmdb)).IsFalse();
	}

	[Test]
	public async Task Reject_AfterARead_FailsTheNextRead()
	{
		await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		Testee.Reject(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsError();
		await Assert.That(Testee.IsRejected(ApiKeyProvider.Tmdb)).IsTrue();
	}

	[Test]
	public async Task GetOrLoadAsync_AfterARejection_FailsAsARejectedSecret()
	{
		Testee.Reject(ApiKeyProvider.Tmdb);

		var read = await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check);

		var error = await Assert.That(read).IsError();
		await Assert.That(error!.CausedBy<SecretRejectedException>()).IsSome();
	}

	[Test]
	public async Task Put_AfterARejection_ServesTheSecretAgain()
	{
		Testee.Reject(ApiKeyProvider.Tmdb);

		Testee.Put(ApiKeyProvider.Tmdb, Stored);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsSuccess().And.IsEqualTo(Stored);
	}

	[Test]
	public async Task Evict_AfterARejection_LoadsAgain()
	{
		Testee.Reject(ApiKeyProvider.Tmdb);

		Testee.Evict(ApiKeyProvider.Tmdb);

		await Assert.That(await Testee.GetOrLoadAsync(ApiKeyProvider.Tmdb, Load, Check)).IsSuccess();
		await Assert.That(_loads).IsEqualTo(1);
	}

	private Task<Result<IApiKey>> Load()
	{
		_loads++;
		return Task.FromResult<Result<IApiKey>>(Success.Of(Stored));
	}

	private Task<Result<IApiKey>> Fail()
	{
		_loads++;
		return Task.FromResult<Result<IApiKey>>(Error.From("No key is stored."));
	}

	private static Task<AccessCheck> Check(IApiKey key) => Task.FromResult(AccessCheck.Valid);

	private static Func<IApiKey, Task<AccessCheck>> Answering(AccessCheck answer) => _ => Task.FromResult(answer);
}
