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
			new NotificationTarget(Guid.Empty, "My Webhook", true, new WebhookConfig(new Secret("https://example.test/hook"), new Secret("whsec_topsecret")))
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
				await Assert.That(webhook.Url.Reveal()).IsEqualTo("https://example.test/hook");
				await Assert.That(webhook.SigningSecret.Reveal()).IsEqualTo("whsec_topsecret");
			});
	}

	[Test]
	public async Task CreateAsync_ForAPushoverTarget_RoundTripsTheDecryptedConfig()
	{
		var id = await _testee.CreateAsync(
			new NotificationTarget(Guid.Empty, "My Pushover", true, new PushoverConfig(new Secret("app-token"), new Secret("user-key")))
		);

		var target = await Assert.That(await _testee.FindAsync(id)).IsSome();

		await Assert.That(target!.Channel).IsEqualTo(NotificationChannel.Pushover);
		await Assert
			.That(target.Config)
			.IsOfUnionType<NotificationConfig, PushoverConfig>()
			.And.Multiple(async pushover =>
			{
				await Assert.That(pushover.AppToken.Reveal()).IsEqualTo("app-token");
				await Assert.That(pushover.UserKey.Reveal()).IsEqualTo("user-key");
			});
	}

	[Test]
	public async Task CreateAsync_WhenATargetIsStored_PersistsTheChannelAsAString()
	{
		await _testee.CreateAsync(
			new NotificationTarget(Guid.Empty, "Webhook", true, new WebhookConfig(new Secret("https://example.test/hook"), new Secret("whsec_x")))
		);

		var channel = (await fixture.RawContext.Table<NotificationTargetRow>().QueryAsync<string>(nameof(NotificationTargetRow.Channel))).Single();

		await Assert.That(channel).IsEqualTo(nameof(NotificationChannel.Webhook));
	}

	[Test]
	public async Task UpdateAsync_WhenATargetChanges_UpdatesNameEnabledAndConfig()
	{
		var id = await _testee.CreateAsync(
			new NotificationTarget(Guid.Empty, "Original", true, new WebhookConfig(new Secret("https://example.test/one"), new Secret("whsec_one")))
		);

		await _testee.UpdateAsync(new NotificationTarget(id, "Renamed", false, new WebhookConfig(new Secret("https://example.test/two"), new Secret("whsec_two"))));

		var target = await Assert.That(await _testee.FindAsync(id)).IsSome();
		await Assert.That(target!.Name).IsEqualTo("Renamed");
		await Assert.That(target.Enabled).IsFalse();
		await Assert
			.That(target.Config)
			.IsOfUnionType<NotificationConfig, WebhookConfig>()
			.And.Multiple(async webhook => await Assert.That(webhook.Url.Reveal()).IsEqualTo("https://example.test/two"));
	}

	[Test]
	public async Task DeleteAsync_WhenATargetExists_RemovesIt()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Doomed", true, new PushoverConfig(new Secret("token"), new Secret("key"))));

		await _testee.DeleteAsync(id);

		await Assert.That(await _testee.FindAsync(id)).IsNone();
	}

	[Test]
	public async Task Targets_WhenTwoUsersExist_AreIsolatedPerUser()
	{
		var id = await _testee.CreateAsync(new NotificationTarget(Guid.Empty, "Owner A", true, new PushoverConfig(new Secret("token"), new Secret("key"))));

		fixture.CurrentUser.Id = UserId.From(Guid.NewGuid());
		await Assert.That(await _testee.GetAllAsync()).IsEmpty();

		fixture.CurrentUser.Id = TestCurrentUser.DefaultId;
		var mine = await _testee.GetAllAsync();
		await Assert.That(mine).Count().IsEqualTo(1);
		await Assert.That(mine[0].Id).IsEqualTo(id);
	}
}
