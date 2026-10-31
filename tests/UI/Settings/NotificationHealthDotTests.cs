using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationHealthDotTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		var clock = new FakeTimeProvider(Now);
		Services.AddSingleton<TimeProvider>(clock);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Popover_ForARetryingDelivery_SaysWhenItWasRecordedAndWhenItRetries()
	{
		var retrying = new NotificationDeliveryOutcome(
			new Retrying("HTTP 500", Now.AddMinutes(10)),
			NotificationEventType.WantlistSyncCompleted,
			Now.AddMinutes(-16)
		);

		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<NotificationHealthDot>(1);
			builder.AddComponentParameter(2, nameof(NotificationHealthDot.Outcomes), new[] { retrying });
			builder.CloseComponent();
		});
		await cut.Find("[aria-label^='Delivery health']").ClickAsync(new());

		cut.WaitForState(() => cut.Markup.Contains("Recent deliveries", StringComparison.Ordinal));

		await Assert.That(cut.Markup).Contains("16 minutes ago").And.Contains("retry 10 minutes from now · HTTP 500");
	}

	[Test]
	public async Task Dot_OnAPhoneWithNoDeliveries_OpensASheetSayingSo()
	{
		var cut = this.RenderOnPhone<NotificationHealthDot>(parameters => parameters.Add(dot => dot.Outcomes, []));

		await cut.Component.Find("button[aria-label='No deliveries yet']").ClickAsync();

		var sheet = cut.Portal.WaitForElement("[data-sheet][aria-label='Deliveries']");
		await Assert.That(sheet.TextContent).Contains("No deliveries yet");
	}

	[Test]
	public async Task Dot_WhenNotInteractive_OffersNothingToTapAndNamesAFailureInItsColour()
	{
		var failed = new NotificationDeliveryOutcome(new Failed("HTTP 500"), NotificationEventType.WantlistSyncCompleted, Now.AddMinutes(-3));

		var cut = Render<NotificationHealthDot>(parameters => parameters.Add(dot => dot.Outcomes, [failed]).Add(dot => dot.Interactive, false));

		await Assert.That(cut.FindAll("button")).IsEmpty();
		await Assert.That(cut.Find("[aria-hidden=true]").ClassList).Contains("bg-destructive");
		await Assert.That(cut.Find(".sr-only").TextContent).IsEqualTo("A delivery failed");
	}
}
