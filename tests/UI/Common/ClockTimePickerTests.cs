using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class ClockTimePickerTests : BunitContext
{
	private TimeSpan? _set;

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Phone_HourThenMinute_SetsTheTimeOnlyOnSet()
	{
		var picker = PhonePicker(new TimeSpan(6, 30, 0));
		await Assert.That(picker.Component.Find(".clock-field").TextContent.Trim()).IsEqualTo("06:30");

		await picker.Component.Find(".clock-field").ClickAsync(new());
		await picker.Portal.WaitForElement("[aria-label='Hour 07']").ClickAsync(new());
		await Assert.That(picker.Portal.Find("[role=dialog]").TextContent).Contains("Pick the minute");
		await picker.Portal.Find("[aria-label='Minute 45']").ClickAsync(new());
		await Assert.That(_set).IsNull();

		var set = picker.Portal.Find("[data-dialog-commit]");
		await Assert.That(set.TextContent.Trim()).IsEqualTo("Set 07:45");
		await set.ClickAsync(new());

		await Assert.That(_set).IsEqualTo(new TimeSpan(7, 45, 0));
	}

	[Test]
	public async Task Phone_Cancel_KeepsTheTime()
	{
		var picker = PhonePicker(new TimeSpan(6, 30, 0));

		await picker.Component.Find(".clock-field").ClickAsync(new());
		await picker.Portal.WaitForElement("[aria-label='Hour 09']").ClickAsync(new());
		await picker.Portal.Find("[data-dialog-cancel]").ClickAsync(new());

		await Assert.That(_set).IsNull();
		await Assert.That(picker.Component.Find(".clock-field").TextContent.Trim()).IsEqualTo("06:30");
	}

	[Test]
	public async Task Desktop_KeepsTheTypedFieldAndSetsTheTimeOnceTheMinuteIsPicked()
	{
		TimeSpan? set = null;
		var portal = Render<BbPortalHost>();
		var cut = Render<ClockTimePicker>(parameters =>
			parameters.Add(picker => picker.Value, new TimeSpan(6, 30, 0)).Add(picker => picker.ValueChanged, time => set = time)
		);
		await Assert.That(cut.FindComponents<BbTimeInput>()).HasSingleItem();

		await cut.Find("[aria-label='Open time picker']").ClickAsync(new());
		await portal.WaitForElement("[aria-label='Hour 22']").ClickAsync(new());
		await portal.Find("[aria-label='Minute 15']").ClickAsync(new());

		await Assert.That(set).IsEqualTo(new TimeSpan(22, 15, 0));
	}

	private PhoneRender<ClockTimePicker> PhonePicker(TimeSpan time) =>
		this.RenderOnPhone<ClockTimePicker>(parameters => parameters.Add(picker => picker.Value, time).Add(picker => picker.ValueChanged, value => _set = value));
}
