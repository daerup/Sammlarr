using Contracts.Identity;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;
using UI.Shared.Notifications.WebUi;
using UI.Shared.Services;

namespace UI.Tests.Notifications.WebUi;

public class WebUiChannelTests
{
	private static readonly UserId Recipient = UserId.New();

	private readonly WebUiCircuits circuits = new();
	private readonly List<Toast> shown = [];

	private Result<RenderedNotification> rendered = Success.Of(RenderedNotification.Empty);

	private WebUiChannel Testee
	{
		get
		{
			var recipient = ICurrentUser.Mock();
			recipient.Id.Returns(Recipient);
			var renderer = INotificationTemplateRenderer.Mock();
			renderer.Render(Any<NotificationTemplate>(), Any()).Returns(rendered);
			return new WebUiChannel(circuits, recipient.Object, renderer.Object, NullLogger<WebUiChannel>.Instance);
		}
	}

	[Before(Test)]
	public void OpenTheRecipientsCircuit() => circuits.Register(Recipient, shown.Add);

	[Test]
	public async Task SendAsync_WhenTheTemplateRenders_ShowsTheRenderedTitleAndMessage()
	{
		rendered = Success.Of(new RenderedNotification(Some.Of("Nightly run"), Some.Of("42 items added"), new None()));

		var result = await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted), Target(), NotificationTemplate.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		await Assert.That(shown).HasSingleItem();
		await Assert.That(shown[0].Title).IsEqualTo("Nightly run");
		await Assert.That(shown[0].Message).IsEqualTo("42 items added");
	}

	[Test]
	public async Task SendAsync_WhenTheTemplateIsEmpty_FallsBackToTheEventTitleAndSummary()
	{
		var result = await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted), Target(), NotificationTemplate.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliverySucceeded>();
		await Assert.That(shown[0].Title).IsEqualTo("Feed run completed");
		await Assert.That(shown[0].Message).IsEqualTo("42 items");
	}

	[Test]
	public async Task SendAsync_WhenNoLevelIsConfigured_ShowsTheNotificationsOwnLevelRatherThanOneDerivedFromItsType()
	{
		await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted, NotificationLevel.Error), Target(), NotificationTemplate.None);

		await Assert.That(shown[0].Level).IsEqualTo(NotificationLevel.Error);
	}

	[Test]
	[Arguments(NotificationLevel.Info)]
	[Arguments(NotificationLevel.Warning)]
	[Arguments(NotificationLevel.Error)]
	public async Task SendAsync_WhenALevelIsConfigured_OverridesTheDerivedLevel(NotificationLevel configured)
	{
		var template = NotificationTemplate.None with { Level = Some.Of(configured) };

		await Testee.SendAsync(Context(NotificationEventType.FeedRunFailed), Target(), template);

		await Assert.That(shown[0].Level).IsEqualTo(configured);
	}

	[Test]
	public async Task SendAsync_WhenTheTargetIsAnotherChannel_FailsUnrecoverablyAndShowsNothing()
	{
		var target = new NotificationTarget(Guid.NewGuid(), "Hook", true, new WebhookConfig("https://example.test", "secret"));

		var result = await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted), target, NotificationTemplate.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(shown).IsEmpty();
	}

	[Test]
	public async Task SendAsync_WhenTheTemplateIsInvalid_FailsUnrecoverablyAndShowsNothing()
	{
		rendered = Error.From("The template is not valid.");

		var result = await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted), Target(), NotificationTemplate.None);

		await Assert.That(result).IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>();
		await Assert.That(shown).IsEmpty();
	}

	[Test]
	public async Task SendAsync_WhenAnotherUserHasACircuitOpen_ShowsNothingToThem()
	{
		var others = new List<Toast>();
		circuits.Register(UserId.New(), others.Add);

		await Testee.SendAsync(Context(NotificationEventType.FeedRunCompleted), Target(), NotificationTemplate.None);

		await Assert.That(shown).HasSingleItem();
		await Assert.That(others).IsEmpty();
	}

	private static NotificationTarget Target() => new(Guid.NewGuid(), "This app", true, new WebUiConfig());

	private static NotificationContext Context(NotificationEventType type, NotificationLevel level = NotificationLevel.Info) =>
		new(Guid.NewGuid(), type, level, DateTimeOffset.UnixEpoch, Title(type), "42 items", new NotificationData());

	private static string Title(NotificationEventType type) =>
		type switch
		{
			NotificationEventType.FeedRunCompleted => "Feed run completed",
			NotificationEventType.FeedRunFailed => "Feed run failed",
			NotificationEventType.ProviderRateLimited => "Provider rate limited",
			NotificationEventType.WatchlistSyncCompleted => "Watchlist sync completed",
			NotificationEventType.WatchlistSyncFailed => "Watchlist sync failed",
			NotificationEventType.TestPing => "Test ping",
		};
}
