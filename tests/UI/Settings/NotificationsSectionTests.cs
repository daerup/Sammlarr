using BlazorBlueprint.Components;
using Bunit;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UI.Features.Settings.Components.Notifications;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class NotificationsSectionTests : BunitContext
{
	private static readonly NotificationTarget Toasts = new(Guid.NewGuid(), "This app", false, new WebUiConfig());

	private readonly Mock<INotificationTargetRepository> _targets = INotificationTargetRepository.Mock();

	private readonly Mock<INotificationSubscriptionRepository> _subscriptions = INotificationSubscriptionRepository.Mock();

	private readonly Mock<INotificationDeliveryOutcomeRepository> _outcomes = INotificationDeliveryOutcomeRepository.Mock();

	[Before(Test)]
	public void Setup()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddLogging();
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
		Services.AddSingleton(_targets.Object);
		Services.AddSingleton(_subscriptions.Object);
		Services.AddSingleton(_outcomes.Object);
		Services.AddSingleton(INotificationTemplateRenderer.Mock().Object);
		_targets.GetAllAsync(Any()).Returns([Toasts]);
		_subscriptions.GetForTargetAsync(Any(), Any()).Returns([]);
		_outcomes.GetRecentByTargetAsync(Any(), Any()).Returns(new Dictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>());
	}

	[Test]
	public async Task Client_OnAPhone_ShowsItsNameAndDeliveryStateAndUnfoldsTheRest()
	{
		var cut = this.RenderOnPhone<NotificationsSection>(_ => { }).Component;
		var client = cut.WaitForElement("[data-client]");
		var summary = client.QuerySelector(".unfold-row-trigger")!;

		await Assert.That(summary.TextContent).Contains("This app").And.Contains("No deliveries yet").And.DoesNotContain("In-app toasts");
		await Assert.That(summary.QuerySelectorAll("[role=switch], button")).IsEmpty();
		await Assert.That(client.TextContent).Contains("In-app toasts · disabled");
		await Assert.That(client.QuerySelectorAll("[role=switch]").Length).IsEqualTo(1);
	}

	[Test]
	public async Task Client_OnADesktop_KeepsItsControlsInTheRow()
	{
		var cut = Render<NotificationsSection>();
		var client = cut.WaitForElement("[data-client]");

		await Assert.That(client.TextContent).Contains("In-app toasts · disabled");
		await Assert.That(client.QuerySelectorAll("[role=switch]").Length).IsEqualTo(1);
		await Assert.That(client.QuerySelectorAll(".unfold-row-trigger")).IsEmpty();
	}
}
