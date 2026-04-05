using Contracts.Auth;
using Contracts.Caching;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Stores;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class CredentialStoreTests(SQLiteFixture fixture)
{
	private ICredentialStore _testee = null!;
	private Mock<ITypedMemoryCache<AuthProvider, Credential>> _cache = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_cache = ITypedMemoryCache<AuthProvider, Credential>.Mock();
		_testee = new CredentialStore(fixture.Context, _cache.Object);
	}

	[Test]
	public async Task GetCredential_WhenNotStored_ReturnsNull()
	{
		var credential = await _testee.GetAsync(AuthProvider.Plex);

		await Assert.That(credential).IsNull();
	}

	[Test]
	public async Task StoreCredential_StoresAndRetrievesSuccessfully()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var accessToken = "test-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, accessToken, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.AuthProvider).IsEqualTo(authProvider);
		await Assert.That(credential.AccessToken).IsEqualTo(accessToken);
		await Assert.That(credential.ExpiresAt).IsEqualTo(expiresAt);
		await Assert.That(credential.ClientId).IsEqualTo(clientId);
	}

	[Test]
	public async Task StoreCredential_WithEncryption_StoresEncryptedInDatabase()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var plainToken = "secret-access-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, plainToken, expiresAt, clientId);

		var encrypted = (await fixture.RawContext.Table<Credential>().QueryAsync(x => x.AccessToken)).Single();

		await Assert.That(encrypted).IsNotEqualTo(plainToken);
		await Assert.That(encrypted).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task StoreCredential_WithClientIdEncryption_StoresEncryptedClientIdInDatabase()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var encryptedClientId = (await fixture.RawContext.Table<Credential>().QueryAsync<string>(nameof(Credential.ClientId))).Single();

		await Assert.That(encryptedClientId).IsNotEqualTo(clientId.ToString());
		await Assert.That(encryptedClientId).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task StoreCredential_WithClientIdEncryption_RetrievesDecryptedGuid()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);
		fixture.Context.ChangeTracker.Clear();

		var credential = await _testee.GetAsync(authProvider);

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential!.ClientId).IsEqualTo(clientId);
	}

	[Test]
	[Arguments("simple-token")]
	[Arguments("token-with-dashes-123")]
	[Arguments("special!@#$%^&*()")]
	[Arguments("unicode-café")]
	[Arguments("")]
	public async Task StoreCredential_WithVariousTokens_PreservesPlaintext(string token)
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.AccessToken).IsEqualTo(token);
		await Assert.That(credential.AuthProvider).IsEqualTo(authProvider);
	}

	[Test]
	public async Task UpdateCredential_WithNewToken_UpdatesValue()
	{
		var authProvider = AuthProvider.Plex;
		var clientId1 = Guid.NewGuid();
		var clientId2 = Guid.NewGuid();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
		var newToken = "new-token";

		await SetUpCredentialAsync(authProvider, "old-token", expiresAt, clientId1);
		await _testee.StoreAsync(authProvider, newToken, expiresAt, clientId2);

		var storedKeys = await fixture.Context.Credentials.ToListAsync();

		await Assert.That(storedKeys).IsNotNull();
		await Assert.That(storedKeys).Count().IsEqualTo(1);
		await Assert
			.That(storedKeys.Single())
			.Satisfies(c =>
				c is not null && c.AuthProvider == authProvider && c.AccessToken == newToken && c.ClientId == clientId2 && c.ExpiresAt == expiresAt
			);
	}

	[Test]
	public async Task ClearCredential_RemovesStoredData()
	{
		var authProvider = AuthProvider.Plex;
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
		var authProvider = AuthProvider.Plex;

		await _testee.GetAsync(authProvider);

		_cache.TryGetValue(authProvider).WasCalled(Times.Once);
	}

	[Test]
	public async Task StoreAsync_AddsToCache()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await _testee.StoreAsync(authProvider, token, expiresAt, clientId);

		_cache.Set(authProvider, Is<Credential>(MatchCredential)).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
		return;

		bool MatchCredential(Credential? c) =>
			c is not null && c.AuthProvider == authProvider && c.AccessToken == token && c.ClientId == clientId && c.ExpiresAt == expiresAt;
	}

	[Test]
	public async Task ClearAsync_RemovesFromCache()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);
		_cache.Reset();

		await _testee.ClearAsync(authProvider);

		_cache.Remove(authProvider).WasCalled(Times.Once);
		_cache.VerifyNoOtherCalls();
	}

	private async Task SetUpCredentialAsync(AuthProvider authProvider, string accessToken, DateTimeOffset expiresAt, Guid clientId)
	{
		await fixture.Context.Credentials.AddAsync(
			new Credential
			{
				AuthProvider = authProvider,
				AccessToken = accessToken,
				ExpiresAt = expiresAt,
				ClientId = clientId,
			}
		);
		await fixture.Context.SaveChangesAsync();
	}

	private static bool IsBase64OrEmpty(string? value)
	{
		if (value is null)
			return false;
		if (string.IsNullOrEmpty(value))
			return true;

		Span<byte> buffer = new Span<byte>(new byte[value.Length]);
		return Convert.TryFromBase64String(value, buffer, out _);
	}
}
