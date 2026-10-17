using System.Globalization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class SchedulePickerTests : BunitContext
{
	private string? _schedule = "untouched";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Description_InAGermanBrowser_StaysEnglishLikeTheRestOfTheUi()
	{
		var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
		CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
		try
		{
			var cut = Render<SchedulePicker>(parameters => parameters.Add(picker => picker.CronExpression, "0 0 23 * * 6"));

			await Assert.That(cut.Find("span.italic").TextContent).Contains("At 23:00, only on Saturday").And.DoesNotContain("Samstag");
		}
		finally
		{
			(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
		}
	}

	[Test]
	public async Task TypedTime_InAnEmptyDailyPicker_BecomesTheSchedule()
	{
		var cut = Picker(null);

		cut.Find("input[aria-label=Hour]").Input("7");
		cut.Find("input[aria-label=Minute]").Input("35");

		await Assert.That(_schedule).IsEqualTo("0 35 7 * * *");
	}

	[Test]
	public async Task TypedMinute_OffTheFiveMinuteStep_IsKept()
	{
		var cut = Picker("0 0 23 * * *");

		cut.Find("input[aria-label=Minute]").Input("23");

		await Assert.That(_schedule).IsEqualTo("0 23 23 * * *");
	}

	[Test]
	public async Task TypedTime_InAWeeklyPicker_KeepsTheDays()
	{
		var cut = Picker("0 0 23 * * 1,6");

		cut.Find("input[aria-label=Hour]").Input("6");

		await Assert.That(_schedule).IsEqualTo("0 0 6 * * 1,6");
	}

	[Test]
	public async Task HourTyped_WithoutMinutes_IsNoScheduleYet()
	{
		var cut = Picker("0 30 3 * * *");

		cut.Find("input[aria-label=Minute]").Input("");

		await Assert.That(_schedule).IsNull();
		await Assert.That(cut.FindAll("span.italic")).IsEmpty();
	}

	[Test]
	public async Task Clock_OpensTheSteppingPicker_WhoseChoiceReplacesTheTypedTime()
	{
		var cut = Picker("0 0 23 * * *");

		await Assert.That(cut.FindAll("button[aria-label='Open time picker']")).Count().IsEqualTo(1);
		cut.Find("button[aria-label='Open time picker']").Click();
		cut.FindAll("[role=dialog] button").Single(button => button.TextContent.Trim() == "Clear").Click();

		await Assert.That(_schedule).IsNull();
		await Assert.That(cut.Find("input[aria-label=Hour]").GetAttribute("value")).IsEmpty();
	}

	private IRenderedComponent<IComponent> Picker(string? schedule)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<SchedulePicker>(1);
			builder.AddComponentParameter(2, nameof(SchedulePicker.CronExpression), schedule);
			builder.AddComponentParameter(3, nameof(SchedulePicker.CronExpressionChanged), EventCallback.Factory.Create<string?>(this, value => _schedule = value));
			builder.CloseComponent();
		});
	}
}
