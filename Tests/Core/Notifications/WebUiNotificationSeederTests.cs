using Contracts.Notifications;
using Core.Notifications;
using Domain.Notifications;

namespace Core.Tests.Notifications;

public class WebUiNotificationSeederTests
{
	private readonly Mock<INotificationTargetRepository> targets = INotificationTargetRepository.Mock();
	private readonly Mock<INotificationSubscriptionRepository> subscriptions = INotificationSubscriptionRepository.Mock();

	private WebUiNotificationSeeder Testee => new(targets.Object, subscriptions.Object);

	[Test]
	public async Task SeedAsync_Always_CreatesAnEnabledWebUiTargetNamedAfterTheApp()
	{
		targets.CreateAsync(Any(), Any()).Returns(Guid.NewGuid());

		await Testee.SeedAsync();

		targets
			.CreateAsync(Arg.Is<NotificationTarget>(target => target.Channel == NotificationChannel.WebUi && target.Enabled && target.Name == "Sammlarr"), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task SeedAsync_Always_SubscribesOneSubscriptionToEveryDefaultEventType()
	{
		var created = Guid.NewGuid();
		targets.CreateAsync(Any(), Any()).Returns(created);

		await Testee.SeedAsync();

		subscriptions
			.AddAsync(
				created,
				Arg.Is<IReadOnlyList<NotificationEventType>>(types =>
					types.SequenceEqual(
						new[]
						{
							NotificationEventType.ProviderRateLimited,
							NotificationEventType.WatchlistSynced,
							NotificationEventType.WatchlistSyncFailed,
							NotificationEventType.FeedRunCompleted,
							NotificationEventType.FeedRunFailed,
						}
					)
				),
				Any(),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task SeedAsync_Always_LeavesTheTemplateAutomaticSoEachEventKeepsItsOwnLevel()
	{
		targets.CreateAsync(Any(), Any()).Returns(Guid.NewGuid());

		await Testee.SeedAsync();

		subscriptions.AddAsync(Any(), Any(), Arg.Is<NotificationTemplate>(seeded => seeded == NotificationTemplate.None), Any()).WasCalled(Times.Once);
	}
}
