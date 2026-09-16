using BlazorBlueprint.Components;
using Bunit;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Notifications;
using UI.Shared.Services;

namespace UI.Tests.Settings.WebUi;

public class WebUiAlertEditingTests : BunitContext
{
	private static readonly NotificationTarget Target = new(Guid.NewGuid(), "This app", true, new WebUiConfig());

	[Before(Test)]
	public void Setup()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddLogging();
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(INotificationSubscriptionRepository.Mock().Object);

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
	public async Task Render_WhenEditingAWebUiAlertWithoutALevel_ShowsTheLevelField()
	{
		var cut = Render<NotificationAlertDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Clients, [Target])
				.Add(dialog => dialog.Client, Target)
				.Add(dialog => dialog.Existing, Subscription(new None()))
		);

		await Assert.That(cut.Markup).Contains("Level");
	}

	private static NotificationSubscription Subscription(Optional<NotificationLevel> level) =>
		new(
			Guid.NewGuid(),
			Target.Id,
			[NotificationEventType.ProviderRateLimited],
			NotificationTemplate.None with
			{
				Title = Some.Of("{{ data.provider }} is rate limiting"),
				Message = Some.Of("{{ summary }}"),
				Level = level,
			}
		);
}
