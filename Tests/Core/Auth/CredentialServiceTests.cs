using Contracts.Auth;
using Core.Auth;
using Core.Caching;
using Domain.Auth;
using Domain.Providers;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Auth;

public sealed class CredentialServiceTests : IDisposable
{
	private static readonly StoredCredential Stored = new("plex-token", DateTimeOffset.UnixEpoch.AddYears(60), Guid.NewGuid());

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<ICredentialRepository> _credentials = ICredentialRepository.Mock();

	private readonly CredentialService Testee;

	public CredentialServiceTests()
	{
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(Stored));
		Testee = new CredentialService(
			_credentials.Object,
			new TestCredentialFactory(),
			new SecretCache<AuthProvider, ICredential>(new TypedMemoryCache<(Guid User, AuthProvider Provider), ICredential>(_memory), new TestCurrentUser())
		);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsTheStoredCredentialOnce()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		await Assert
			.That(await Testee.GetAsync(AuthProvider.Plex))
			.IsSuccess()
			.And.Multiple(async credential => await Assert.That(credential.AccessToken).IsEqualTo("plex-token"));
		_credentials.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task IsStoredAsync_ReadsWhetherACredentialIsStored()
	{
		await Assert.That(await Testee.IsStoredAsync(AuthProvider.Plex)).IsTrue();

		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Error.From("No credential is stored for this provider."));

		await Assert.That(await Testee.IsStoredAsync(AuthProvider.Plex)).IsFalse();
	}

	[Test]
	public async Task ReportRejected_AfterARead_MakesTheNextReadLoadTheStoredCredentialAgain()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		Testee.ReportRejected(AuthProvider.Plex);

		await Testee.GetAsync(AuthProvider.Plex);
		_credentials.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task StoreAsync_AfterARead_StoresItAndMakesTheNextReadLoadTheNewOne()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		await Testee.StoreAsync(AuthProvider.Plex, Stored with { AccessToken = "new-token" });

		await Testee.GetAsync(AuthProvider.Plex);
		_credentials.StoreAsync(AuthProvider.Plex, "new-token", Stored.ExpiresAt, Stored.ClientId, Any()).WasCalled(Times.Once);
		_credentials.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task ClearAsync_AfterARead_DropsTheCachedCredential()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		await Testee.ClearAsync(AuthProvider.Plex);

		await Testee.GetAsync(AuthProvider.Plex);
		_credentials.GetAsync(AuthProvider.Plex, Any()).WasCalled(Times.Exactly(2));
	}
}
