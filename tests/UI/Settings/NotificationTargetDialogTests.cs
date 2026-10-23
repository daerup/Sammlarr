using System.Text.RegularExpressions;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.DI;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;
using UI.Features.Settings.Components.Notifications;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class NotificationTargetDialogTests : BunitContext
{
	private const string Url = "https://example.com/hook";

	private readonly Mock<INotificationChannel> _webhook = INotificationChannel.Mock();

	private readonly Mock<INotificationTargetRepository> _targets = INotificationTargetRepository.Mock();

	[Before(Test)]
	public void Setup()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		Services.AddLogging();
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(_targets.Object);
		Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
		Services.AddSingleton<IKeyedServiceIndex<NotificationChannel, INotificationChannel>>(
			new FakeKeyedServiceIndex<NotificationChannel, INotificationChannel>().Register(NotificationChannel.Webhook, _webhook.Object)
		);
		Services.AddScoped<NotificationTester>();
		_webhook.SendAsync(Any(), Any(), Any(), Any()).Returns(new DeliverySucceeded());
	}

	[Test]
	public async Task Test_WhileCreatingAWebhook_PingsItUnsavedWithTheSecretItIsThenSavedWith()
	{
		var cut = Render<NotificationTargetDialog>();
		await TypeAsync(cut, "My webhook", "Home Assistant");
		await TypeAsync(cut, Url, Url);

		await Button(cut, "Test connection").ClickAsync(new());

		await Assert.That(cut.Markup).Contains("Delivered.");
		_targets.CreateAsync(Any(), Any()).WasNeverCalled();

		await Button(cut, "Save").ClickAsync(new());

		var secret = Regex.Match(cut.Markup, "whsec_[A-Za-z0-9+/=]+").Value;
		_webhook.SendAsync(Any(), Is<NotificationTarget>(target => IsTheWebhook(target, secret)), Any(), Any()).WasCalled(Times.Once);
		_targets.CreateAsync(Is<NotificationTarget>(target => IsTheWebhook(target, secret)), Any()).WasCalled(Times.Once);
	}

	private static Task TypeAsync(IRenderedComponent<NotificationTargetDialog> cut, string placeholder, string text) =>
		cut.InvokeAsync(() => cut.FindComponents<BbInput>().Single(input => input.Instance.Placeholder == placeholder).Instance.ValueChanged.InvokeAsync(text));

	private static bool IsTheWebhook(NotificationTarget? target, string secret) =>
		target is { Config: WebhookConfig webhook } && webhook.Url.Reveal() == Url && webhook.SigningSecret.Reveal() == secret;

	private static AngleSharp.Dom.IElement Button(IRenderedComponent<NotificationTargetDialog> cut, string text) =>
		cut.FindAll("button").Single(button => button.TextContent.Trim() == text);
}
