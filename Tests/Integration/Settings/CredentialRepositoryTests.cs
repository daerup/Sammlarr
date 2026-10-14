using Contracts.Auth;
using DB.Data.Auth;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Settings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class CredentialRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private ICredentialRepository Testee => new CredentialRepository(fixture.ContextFactory);

	[Test]
	public async Task GetCredential_WhenNotStored_ReturnsError()
	{
		var credential = await Testee.GetAsync(AuthProvider.Plex);

		await Assert.That(credential).IsError();
	}

	[Test]
	public async Task StoreCredential_StoresAndRetrievesSuccessfully()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var accessToken = "test-token";
		var expiresAt = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);

		await Testee.StoreAsync(authProvider, accessToken, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.Provider).IsEqualTo(authProvider);
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

		await Testee.StoreAsync(authProvider, plainToken, expiresAt, clientId);

		var encrypted = (await fixture.RawContext.Table<CredentialRow>().QueryAsync(x => x.AccessToken)).Single();

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

		await Testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var encryptedClientId = (await fixture.RawContext.Table<CredentialRow>().QueryAsync<string>(nameof(CredentialRow.ClientId))).Single();

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

		var credential = await Testee.GetAsync(authProvider);

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
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await Testee.StoreAsync(authProvider, token, expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.AccessToken).IsEqualTo(token);
		await Assert.That(credential.Provider).IsEqualTo(authProvider);
	}

	[Test]
	public async Task UpdateCredential_WithNewToken_UpdatesValue()
	{
		var authProvider = AuthProvider.Plex;
		var clientId1 = Guid.NewGuid();
		var clientId2 = Guid.NewGuid();
		var expiresAt = new DateTimeOffset(2026, 5, 20, 15, 0, 0, TimeSpan.Zero);
		var newToken = "new-token";

		await SetUpCredentialAsync(authProvider, "old-token", expiresAt, clientId1);
		await Testee.StoreAsync(authProvider, newToken, expiresAt, clientId2);

		var storedKeys = await fixture.Context.Credentials.ToListAsync();

		await Assert.That(storedKeys).IsNotNull();
		await Assert.That(storedKeys).Count().IsEqualTo(1);
		await Assert
			.That(storedKeys.Single())
			.Satisfies(c => c is not null && c.Provider == authProvider && c.AccessToken == newToken && c.ClientId == clientId2 && c.ExpiresAt == expiresAt);
	}

	[Test]
	public async Task ClearCredential_RemovesStoredData()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);

		await Testee.ClearAsync(authProvider);
		var exists = await fixture.Context.Credentials.AnyAsync();

		await Assert.That(exists).IsFalse();
	}

	private async Task SetUpCredentialAsync(AuthProvider provider, string accessToken, DateTimeOffset expiresAt, Guid clientId)
	{
		await fixture.Context.Credentials.AddAsync(
			new CredentialRow
			{
				Provider = provider,
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
