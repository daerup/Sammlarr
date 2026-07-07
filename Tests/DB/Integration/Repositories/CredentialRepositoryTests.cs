using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using DB.Data.Access;
using Domain.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class CredentialRepositoryTests(SQLiteFixture fixture)
{
	private Mock<ITypedMemoryCache<WatchlistProvider, ICredential>> _cache = null!;
	private ICredentialRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<WatchlistProvider, ICredential>.Mock();
		_testee = new CredentialRepository(fixture.ContextFactory, _cache.Object, new TestCredentialFactory());
	}

	[Test]
	public async Task GetCredential_WhenNotStored_ReturnsError()
	{
		var credential = await _testee.GetAsync(WatchlistProvider.Plex);

		await Assert.That(credential).IsError();
	}

	[Test]
	public async Task StoreCredential_StoresAndRetrievesSuccessfully()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var accessToken = "test-token";
		var expiresAt = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);

		await _testee.StoreAsync(authProvider, accessToken, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.WatchlistProvider).IsEqualTo(authProvider);
		await Assert.That(credential.AccessToken).IsEqualTo(accessToken);
		await Assert.That(credential.ExpiresAt).IsEqualTo(expiresAt);
		await Assert.That(credential.ClientId).IsEqualTo(clientId);
	}

	[Test]
	public async Task StoreCredential_WithEncryption_StoresEncryptedInDatabase()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var plainToken = "secret-access-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, plainToken, expiresAt, clientId);

		var encrypted = (await fixture.RawContext.Table<CredentialRow>().QueryAsync(x => x.AccessToken)).Single();

		await Assert.That(encrypted).IsNotEqualTo(plainToken);
		await Assert.That(encrypted).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task StoreCredential_WithClientIdEncryption_StoresEncryptedClientIdInDatabase()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var encryptedClientId = (await fixture.RawContext.Table<CredentialRow>().QueryAsync<string>(nameof(CredentialRow.ClientId))).Single();

		await Assert.That(encryptedClientId).IsNotEqualTo(clientId.ToString());
		await Assert.That(encryptedClientId).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task StoreCredential_WithClientIdEncryption_RetrievesDecryptedGuid()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);
		fixture.Context.ChangeTracker.Clear();

		var credential = await _testee.GetAsync(authProvider);

		await Assert.That(credential).IsSuccess().And.Multiple(async resolved => await Assert.That(resolved.ClientId).IsEqualTo(clientId));
	}

	[Test]
	[Arguments("simple-token")]
	[Arguments("token-with-dashes-123")]
	[Arguments("special!@#$%^&*()")]
	[Arguments("unicode-café")]
	[Arguments("")]
	public async Task StoreCredential_WithVariousTokens_PreservesPlaintext(string token)
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.AccessToken).IsEqualTo(token);
		await Assert.That(credential.WatchlistProvider).IsEqualTo(authProvider);
	}

	[Test]
	public async Task UpdateCredential_WithNewToken_UpdatesValue()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId1 = Guid.NewGuid();
		var clientId2 = Guid.NewGuid();
		var expiresAt = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);
		var newToken = "new-token";

		await SetUpCredentialAsync(authProvider, "old-token", expiresAt, clientId1);
		await _testee.StoreAsync(authProvider, newToken, expiresAt, clientId2);

		var storedKeys = await fixture.Context.Credentials.ToListAsync();

		await Assert.That(storedKeys).IsNotNull();
		await Assert.That(storedKeys).Count().IsEqualTo(1);
		await Assert
			.That(storedKeys.Single())
			.Satisfies(c => c is not null && c.WatchlistProvider == authProvider && c.AccessToken == newToken && c.ClientId == clientId2 && c.ExpiresAt == expiresAt);
	}

	[Test]
	public async Task ClearCredential_RemovesStoredData()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);

		await _testee.ClearAsync(authProvider);
		var exists = await fixture.Context.Credentials.AnyAsync();

		await Assert.That(exists).IsFalse();
	}

	[Test]
	public async Task GetAsync_ReadsFromCache()
	{
		var authProvider = WatchlistProvider.Plex;

		await _testee.GetAsync(authProvider);

		_cache.TryGetValue(authProvider).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_InvalidatesCache()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		_cache.Remove(authProvider).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	[Test]
	public async Task ClearAsync_RemovesFromCache()
	{
		var authProvider = WatchlistProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);
		_cache.Reset();

		await _testee.ClearAsync(authProvider);

		_cache.Remove(authProvider).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	private async Task SetUpCredentialAsync(WatchlistProvider watchlistProvider, string accessToken, DateTimeOffset expiresAt, Guid clientId)
	{
		await fixture.Context.Credentials.AddAsync(
			new CredentialRow
			{
				WatchlistProvider = watchlistProvider,
				AccessToken = accessToken,
				ExpiresAt = expiresAt,
				ClientId = clientId,
			}
		);
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
