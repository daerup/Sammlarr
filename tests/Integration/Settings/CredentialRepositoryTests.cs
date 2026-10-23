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

		await Testee.StoreAsync(authProvider, new Secret(accessToken), expiresAt, clientId);

		var credential = await fixture.Context.Credentials.SingleAsync();

		await Assert.That(credential).IsNotNull();
		await Assert.That(credential.Provider).IsEqualTo(authProvider);
		await Assert.That(credential.AccessToken.Reveal()).IsEqualTo(accessToken);
		await Assert.That(credential.ExpiresAt).IsEqualTo(expiresAt);
		await Assert.That(credential.ClientId).IsEqualTo(clientId);
	}

	[Test]
	public async Task GetCredential_WhenStored_ReadsItBack()
	{
		var authProvider = AuthProvider.Plex;
		var clientId = Guid.NewGuid();
		var token = "secret-token";
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		await SetUpCredentialAsync(authProvider, token, expiresAt, clientId);
		fixture.Context.ChangeTracker.Clear();

		var credential = await Testee.GetAsync(authProvider);

		await Assert
			.That(credential)
			.IsSuccess()
			.And.Multiple(async resolved =>
			{
				await Assert.That(resolved.ClientId).IsEqualTo(clientId);
				await Assert.That(resolved.AccessToken.Reveal()).IsEqualTo(token);
			});
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
		await Testee.StoreAsync(authProvider, new Secret(newToken), expiresAt, clientId2);

		var storedKeys = await fixture.Context.Credentials.ToListAsync();

		await Assert.That(storedKeys).IsNotNull();
		await Assert.That(storedKeys).Count().IsEqualTo(1);
		await Assert
			.That(storedKeys.Single())
			.Satisfies(c => c is not null && c.Provider == authProvider && c.AccessToken.Reveal() == newToken && c.ClientId == clientId2 && c.ExpiresAt == expiresAt);
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
				AccessToken = new Secret(accessToken),
				ExpiresAt = expiresAt,
				ClientId = clientId,
			}
		);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
	}
}
