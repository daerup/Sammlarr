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
	public async Task SeedAsync_Always_CreatesAnEnabledWebUiTarget()
	{
		targets.CreateAsync(Any(), Any()).Returns(Guid.NewGuid());

		await Testee.SeedAsync();

		targets.CreateAsync(Arg.Is<NotificationTarget>(target => target.Channel == NotificationChannel.WebUi && target.Enabled), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SeedAsync_Always_SubscribesTheTargetToRateLimitingOnly()
	{
		var created = Guid.NewGuid();
		targets.CreateAsync(Any(), Any()).Returns(created);

		await Testee.SeedAsync();

		subscriptions
			.AddAsync(
				created,
				Arg.Is<IReadOnlyList<NotificationEventType>>(types => types.SequenceEqual(new[] { NotificationEventType.ProviderRateLimited })),
				Any(),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task SeedAsync_Always_SeedsAWarningTemplateNamingTheProvider()
	{
		targets.CreateAsync(Any(), Any()).Returns(Guid.NewGuid());

		await Testee.SeedAsync();

		subscriptions
			.AddAsync(
				Any(),
				Any(),
				Arg.Is<NotificationTemplate>(seeded =>
					seeded.Title.GetValueOr(string.Empty).Contains("{{ data.provider }}")
					&& seeded.Message.GetValueOr(string.Empty).Contains("{{ summary }}")
					&& seeded.Level.GetValueOr(NotificationLevel.Info) == NotificationLevel.Warning
				),
				Any()
			)
			.WasCalled(Times.Once);
	}
}
