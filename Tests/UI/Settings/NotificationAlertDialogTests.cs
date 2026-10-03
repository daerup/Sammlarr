using BlazorBlueprint.Components;
using Bunit;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Notifications;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class NotificationAlertDialogTests : BunitContext
{
	private static readonly NotificationTarget Target = new(Guid.NewGuid(), "This app", true, new WebUiConfig());

	private readonly Mock<INotificationSubscriptionRepository> _subscriptions = INotificationSubscriptionRepository.Mock();

	[Before(Test)]
	public void Setup()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddLogging();
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(_subscriptions.Object);

		var renderer = INotificationTemplateRenderer.Mock();
		renderer.Render(Any<NotificationTemplate>(), Any()).Returns(Success.Of(RenderedNotification.Empty));
		Services.AddSingleton(renderer.Object);
	}

	[Test]
	public async Task Render_WhenEditingAWebUiAlertWithALevel_ShowsTheLevelField()
	{
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, Subscription(Some.Of(NotificationLevel.Warning)))
		);

		await Assert.That(cut.Markup).Contains("Level");
	}

	[Test]
	public async Task Render_WhenNoLevelIsConfigured_PreviewsTheLevelDerivedFromTheEvent()
	{
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, Subscription(new None()))
		);

		await Assert.That(cut.FindAll("div.font-mono").Last().TextContent.Trim()).IsEqualTo(nameof(NotificationLevel.Warning));
	}

	[Test]
	public async Task Render_WhenNoLevelIsConfigured_LabelsTheSelectionInsteadOfBlanking()
	{
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, Subscription(new None()))
		);

		await Assert.That(cut.Find("button[role=combobox]").TextContent.Trim()).IsEqualTo("Automatic");
	}

	[Test]
	public async Task Render_WhenALevelIsConfigured_PreviewsThatLevel()
	{
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, Subscription(Some.Of(NotificationLevel.Error)))
		);

		await Assert.That(cut.FindAll("div.font-mono").Last().TextContent.Trim()).IsEqualTo(nameof(NotificationLevel.Error));
	}

	[Test]
	public async Task Save_ForAWebUiAlert_KeepsItsTextAndLevelAndStoresNoPriority()
	{
		var existing = Subscription(Some.Of(NotificationLevel.Error), Some.Of(PushoverPriority.High));
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, existing)
		);

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").ClickAsync(new());

		_subscriptions
			.UpdateAsync(existing.Id, Any(), Is<NotificationTemplate>(template => template == existing.Template with { Priority = new None() }), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task Save_ForAPushoverAlert_KeepsItsTextAndPriorityAndStoresNoLevel()
	{
		var pushover = new NotificationTarget(Guid.NewGuid(), "Phone", true, new PushoverConfig("app-token", "user-key"));
		var existing = Subscription(Some.Of(NotificationLevel.Error), Some.Of(PushoverPriority.High)) with { TargetId = pushover.Id };
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [pushover])
				.Add(dialog => dialog.Client, pushover)
				.Add(dialog => dialog.Existing, existing)
		);

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Save").ClickAsync(new());

		_subscriptions
			.UpdateAsync(existing.Id, Any(), Is<NotificationTemplate>(template => template == existing.Template with { Level = new None() }), Any())
			.WasCalled(Times.Once);
	}

	private static NotificationSubscription Subscription(Optional<NotificationLevel> level) => Subscription(level, new None());

	private static NotificationSubscription Subscription(Optional<NotificationLevel> level, Optional<PushoverPriority> priority)
	{
		return new NotificationSubscription(
			Guid.NewGuid(),
			Target.Id,
			[NotificationEventType.ProviderRateLimited],
			NotificationTemplate.None with
			{
				Title = Some.Of("{{ data.provider }} is rate limiting"),
				Message = Some.Of("{{ summary }}"),
				Level = level,
				Priority = priority,
			}
		);
	}
}
