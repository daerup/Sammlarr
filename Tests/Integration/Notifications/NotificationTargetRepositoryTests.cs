using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Notifications;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class NotificationTargetRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private INotificationTargetRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new NotificationTargetRepository(fixture.ContextFactory);

	[Test]
	public async Task CreateAsync_ForAWebhookTarget_RoundTripsTheDecryptedConfig()
	{
		var id = await _testee.CreateAsync(
			new NotificationTarget(Guid.Empty, "My Webhook", true, new WebhookConfig("https://example.test/hook", "whsec_topsecret"))
		);

		var target = await Assert.That(await _testee.FindAsync(id)).IsSome();

		await Assert.That(target!.Name).IsEqualTo("My Webhook");
		await Assert.That(target.Channel).IsEqualTo(NotificationChannel.Webhook);
		await Assert.That(target.Enabled).IsTrue();
		await Assert
			.That(target.Config)
			.IsOfUnionType<NotificationConfig, WebhookConfig>()
			.And.Multiple(async webhook =>
			{
				await Assert.That(webhook.Url).IsEqualTo("https://example.test/hook");
				await Assert.That(webhook.SigningSecret).IsEqualTo("whsec_topsecret");
			});
	}

	[Test]
	public async Task CreateAsync_ForAPushoverTarget_RoundTripsTheDecryptedConfig()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "My Pushover", true, new PushoverConfig("app-token", "user-key")));

		var target = await Assert.That(await _testee.FindAsync(id)).IsSome();

		await Assert.That(target!.Channel).IsEqualTo(NotificationChannel.Pushover);
		await Assert
			.That(target.Config)
			.IsOfUnionType<NotificationConfig, PushoverConfig>()
			.And.Multiple(async pushover =>
			{
				await Assert.That(pushover.AppToken).IsEqualTo("app-token");
				await Assert.That(pushover.UserKey).IsEqualTo("user-key");
			});
	}

	[Test]
	public async Task CreateAsync_ForAWebhookTarget_EncryptsTheSigningSecretAtRest()
	{
		await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Webhook", true, new WebhookConfig("https://example.test/hook", "whsec_plaintext")));

		var stored = (await fixture.RawContext.Table<NotificationTargetRow>().QueryAsync(row => row.SigningSecret)).Single();

		await Assert.That(stored).IsNotEqualTo("whsec_plaintext");
		await Assert.That(stored).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task CreateAsync_ForAPushoverTarget_EncryptsItsSecretsAtRest()
	{
		await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Pushover", true, new PushoverConfig("app-token-plain", "user-key-plain")));

		var appToken = (await fixture.RawContext.Table<NotificationTargetRow>().QueryAsync(row => row.AppToken)).Single();
		var userKey = (await fixture.RawContext.Table<NotificationTargetRow>().QueryAsync(row => row.UserKey)).Single();

		await Assert.That(appToken).IsNotEqualTo("app-token-plain");
		await Assert.That(userKey).IsNotEqualTo("user-key-plain");
		await Assert.That(appToken).Satisfies(IsBase64OrEmpty);
		await Assert.That(userKey).Satisfies(IsBase64OrEmpty);
	}

	[Test]
	public async Task CreateAsync_WhenATargetIsStored_PersistsTheChannelAsAString()
	{
		await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Webhook", true, new WebhookConfig("https://example.test/hook", "whsec_x")));

		var channel = (await fixture.RawContext.Table<NotificationTargetRow>().QueryAsync<string>(nameof(NotificationTargetRow.Channel))).Single();

		await Assert.That(channel).IsEqualTo(nameof(NotificationChannel.Webhook));
	}

	[Test]
	public async Task UpdateAsync_WhenATargetChanges_UpdatesNameEnabledAndConfig()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Original", true, new WebhookConfig("https://example.test/one", "whsec_one")));

		await _testee.UpdateAsync(new NotificationTarget(id, "Renamed", false, new WebhookConfig("https://example.test/two", "whsec_two")));

		var target = await Assert.That(await _testee.FindAsync(id)).IsSome();
		await Assert.That(target!.Name).IsEqualTo("Renamed");
		await Assert.That(target.Enabled).IsFalse();
		await Assert
			.That(target.Config)
			.IsOfUnionType<NotificationConfig, WebhookConfig>()
			.And.Multiple(async webhook => await Assert.That(webhook.Url).IsEqualTo("https://example.test/two"));
	}

	[Test]
	public async Task DeleteAsync_WhenATargetExists_RemovesIt()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Doomed", true, new PushoverConfig("token", "key")));

		await _testee.DeleteAsync(id);

		await Assert.That(await _testee.FindAsync(id)).IsNone();
	}

	[Test]
	public async Task Targets_WhenTwoUsersExist_AreIsolatedPerUser()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Owner A", true, new PushoverConfig("token", "key")));

		fixture.CurrentUser.Id = UserId.From(Guid.NewGuid());
		await Assert.That(await _testee.GetAllAsync()).IsEmpty();

		fixture.CurrentUser.Id = TestCurrentUser.DefaultId;
		var mine = await _testee.GetAllAsync();
		await Assert.That(mine).Count().IsEqualTo(1);
		await Assert.That(mine[0].Id).IsEqualTo(id);
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
