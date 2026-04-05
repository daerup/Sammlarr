using Contracts.Auth;
using DB.Data.Entities;
using DB.Data.Stores;
using Microsoft.Extensions.Caching.Memory;

namespace DB.Tests.Integration;

public class CredentialStoreTests : IAsyncDisposable
{
	private readonly EncryptionTestFixture _fixture = new();
	private SammlarrDbContext Context => field ??= _fixture.CreateDbContext();
	private RawDbContext RawContext => field ??= new RawDbContext(Context);
	private ICredentialStore Store => field ??= new CredentialStore(Context, new MemoryCache(new MemoryCacheOptions()));

	public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

	[Test]
	public async Task GetCredential_WhenNotStored_ReturnsNull()
	{
		var credential = await Store.GetAsync(AuthProvider.Plex);

		await Assert.That(credential).IsNull();
	}

	[Test]
	public async Task StoreCredential_StoresAndRetrievesSuccessfully()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var accessToken = "test-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await Store.StoreAsync(authProvider, accessToken, expiresAt, clientId);

		var credential = await Store.GetAsync(authProvider);

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential!.AccessToken).IsEqualTo(accessToken);
	}

	[Test]
	public async Task StoreCredential_WithEncryption_StoresEncryptedInDatabase()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var plainToken = "secret-access-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await Store.StoreAsync(authProvider, plainToken, expiresAt, clientId);

		var encrypted = (await RawContext.Table<Credential>().QueryAsync(x => x.AccessToken)).Single();

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

		await Store.StoreAsync(authProvider, token, expiresAt, clientId);

		var encryptedClientId = (await RawContext.Table<Credential>().QueryAsync<string>(nameof(Credential.ClientId))).Single();

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

		await Store.StoreAsync(authProvider, token, expiresAt, clientId);

		var credential = await Store.GetAsync(authProvider);

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

		await Store.StoreAsync(authProvider, token, expiresAt, clientId);

		var credential = await Store.GetAsync(authProvider);

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential!.AccessToken).IsEqualTo(token);
	}

	[Test]
	public async Task UpdateCredential_WithNewToken_UpdatesValue()
	{
		var authProvider = AuthProvider.Plex;
		var clientId1 = Guid.NewGuid();
		var clientId2 = Guid.NewGuid();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
		var oldToken = "old-token";
		var newToken = "new-token";

		await Store.StoreAsync(authProvider, oldToken, expiresAt, clientId1);
		await Store.StoreAsync(authProvider, newToken, expiresAt, clientId2);

		var retrieved = await Store.GetAsync(authProvider);

		await Assert.That(retrieved).IsNotNull();
		await Assert.That(retrieved!.AccessToken).IsEqualTo(newToken);
	}

	[Test]
	public async Task ClearCredential_RemovesStoredData()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await Store.StoreAsync(authProvider, token, expiresAt, clientId);

		await Store.ClearAsync(authProvider);
		var storedAfter = await Store.GetAsync(authProvider);

		await Assert.That(storedAfter).IsNull();
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
