using Domain.Feed;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;

namespace Domain.Tests.Notifications;

public class DefaultLevelTests
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
	private static readonly MediaId Media = MediaId.New();

	[Test]
	public async Task DefaultLevel_OfACleanSync_IsSuccess()
	{
		var synced = new WantlistSyncCompletedNotification(Owner, Now, [Media], [], [], [], []);

		await Assert.That(synced.DefaultLevel).IsEqualTo(NotificationLevel.Success);
	}

	[Test]
	public async Task DefaultLevel_OfASyncWithFailedPushes_IsWarningBecauseItOnlyPartlySucceeded()
	{
		var synced = new WantlistSyncCompletedNotification(Owner, Now, [Media], [], [], [], [Media]);

		await Assert.That(synced.DefaultLevel).IsEqualTo(NotificationLevel.Warning);
	}

	[Test]
	public async Task DefaultLevel_OfACompletedFeedRun_IsSuccess()
	{
		var completed = new FeedRunCompletedNotification(Owner, Now, FeedId.From(Guid.NewGuid()), 2, 1);

		await Assert.That(completed.DefaultLevel).IsEqualTo(NotificationLevel.Success);
	}

	[Test]
	public async Task DefaultLevel_OfAFailure_IsError()
	{
		await Assert.That(new WantlistSyncFailedNotification(Owner, Now, "nope").DefaultLevel).IsEqualTo(NotificationLevel.Error);
		await Assert.That(new FeedRunFailedNotification(Owner, Now, FeedId.From(Guid.NewGuid()), "nope").DefaultLevel).IsEqualTo(NotificationLevel.Error);
	}

	[Test]
	public async Task DefaultLevel_OfATestPing_IsInfo()
	{
		var ping = new TestPingNotification(Owner, Now, Guid.NewGuid());

		await Assert.That(ping.DefaultLevel).IsEqualTo(NotificationLevel.Info);
	}

	[Test]
	public async Task DefaultLevel_OfARateLimit_IsWarningBecauseTheRunStillRecovers()
	{
		var limited = new ProviderRateLimitedNotification(Owner, Now, RemoteService.Tmdb, TimeSpan.FromSeconds(30));

		await Assert.That(limited.DefaultLevel).IsEqualTo(NotificationLevel.Warning);
	}
}
