using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Notifications;

namespace UI.Tests.Settings;

public class NotificationHealthDotTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		Services.AddSingleton(clock.Object);
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
}
