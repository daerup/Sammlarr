using System.Globalization;
using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class SchedulePickerTests : BunitContext
{
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
}
