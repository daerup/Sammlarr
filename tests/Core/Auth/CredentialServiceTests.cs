using Contracts.Auth;
using Contracts.Jobs;
using Core.Auth;
using Core.Caching;
using Core.DI;
using Domain.Auth;
using Domain.Jobs;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.Caching.Memory;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Auth;

public sealed class CredentialServiceTests : IDisposable
{
	private static readonly StoredCredential Stored = new(new Secret("plex-token"), DateTimeOffset.UnixEpoch.AddYears(60), Guid.NewGuid());

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly Mock<ICredentialRepository> _credentials = ICredentialRepository.Mock();
	private readonly Mock<ICredentialValidator> _plex = ICredentialValidator.Mock();
	private readonly Mock<IJobQueue> _queue = IJobQueue.Mock();

	private readonly CredentialService Testee;

	public CredentialServiceTests()
	{
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(Stored));
		_plex.CheckAsync(Any(), Any(), Any()).Returns(AccessCheck.Valid);
		Testee = new CredentialService(
			_credentials.Object,
			new KeyedServiceSet<AuthProvider, ICredentialValidator>([new(AuthProvider.Plex, _plex.Object)]),
			new SecretCache<AuthProvider, ICredential>(
				new TypedMemoryCache<(Guid User, AuthProvider Provider), ICredential>(_memory),
				new TypedMemoryCache<(Guid User, AuthProvider Provider), AccessCheck>(_memory),
				new TestCurrentUser()
			),
			_queue.Object
		);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task StoreAsync_QueuesAFillOfMissingDetails()
	{
		await Testee.StoreAsync(AuthProvider.Plex, Stored);

		_queue.EnqueueAsync(Is<IJobRequest>(request => request is FillDetails), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WithoutACallersCause_ChecksTheStoredTokenUnderTheKeyTestCause()
	{
		var cause = UsageCause.Other;
		_plex.CheckAsync(Any(), Any(), Any()).Returns(AccessCheck.Valid).Callback(() => cause = UsageCauses.Current.Cause);

		await Testee.GetAsync(AuthProvider.Plex);

		await Assert.That(cause).IsEqualTo(UsageCause.KeyTest);
	}

	[Test]
	public async Task GetAsync_WhenReadTwice_LoadsTheStoredCredentialOnce()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		await Assert
			.That(await Testee.GetAsync(AuthProvider.Plex))
			.IsSuccess()
			.And.Multiple(async credential => await Assert.That(credential.AccessToken.Reveal()).IsEqualTo("plex-token"));
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
	public async Task ReportRejected_AfterARead_FailsTheNextReadUntilACredentialIsStoredAgain()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		Testee.ReportRejected(AuthProvider.Plex);

		await Assert.That(await Testee.GetAsync(AuthProvider.Plex)).IsError();
		await Testee.StoreAsync(AuthProvider.Plex, Stored with { AccessToken = new Secret("new-token") });
		await Assert.That(await Testee.GetAsync(AuthProvider.Plex)).IsSuccess();
	}

	[Test]
	public async Task GetAsync_WhenPlexCannotBeReached_StillServesTheStoredCredential()
	{
		_plex.CheckAsync(Any(), Any(), Any()).Returns(AccessCheck.Unreachable);

		await Assert.That(await Testee.GetAsync(AuthProvider.Plex)).IsSuccess();
	}

	[Test]
	public async Task TestAsync_ForATokenPlexRefuses_SaysItIsNotValid()
	{
		_plex.CheckAsync(Any(), new Secret("typed-token"), Any()).Returns(AccessCheck.Rejected);

		await Assert
			.That(await Testee.TestAsync(AuthProvider.Plex, Stored with { AccessToken = new Secret("typed-token") }))
			.IsError()
			.And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("The Plex token is not valid."));
	}

	[Test]
	public async Task TestAsync_ForATokenPlexAccepts_IsThatToken()
	{
		await Assert
			.That(await Testee.TestAsync(AuthProvider.Plex, Stored with { AccessToken = new Secret("typed-token") }))
			.IsSuccess()
			.And.Multiple(async credential => await Assert.That(credential.AccessToken.Reveal()).IsEqualTo("typed-token"));
	}

	[Test]
	public async Task StoreAsync_AfterARead_StoresItAndMakesTheNextReadLoadTheNewOne()
	{
		await Testee.GetAsync(AuthProvider.Plex);

		await Testee.StoreAsync(AuthProvider.Plex, Stored with { AccessToken = new Secret("new-token") });

		await Testee.GetAsync(AuthProvider.Plex);
		_credentials.StoreAsync(AuthProvider.Plex, new Secret("new-token"), Stored.ExpiresAt, Stored.ClientId, Any()).WasCalled(Times.Once);
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
