using Bunit;
using TUnit.Assertions.Enums;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class ClockDialTests : BunitContext
{
	[Test]
	public async Task HourDial_ShowsTwelveOutsideAndMidnightInside_AndMarksTheHourPicked()
	{
		var cut = Render<ClockDial>(parameters => parameters.Add(dial => dial.Kind, ClockDialKind.Hour).Add(dial => dial.Value, 18));

		var marks = cut.FindAll(".clock-dial-mark");
		await Assert.That(marks).Count().IsEqualTo(24);
		await Assert
			.That(marks.Where(mark => !mark.ClassList.Contains("is-inner")).Select(mark => mark.TextContent))
			.IsEquivalentTo(Enumerable.Range(1, 12).Select(hour => hour.ToString()));
		await Assert.That(cut.Find("[aria-pressed=true]").GetAttribute("aria-label")).IsEqualTo("Hour 18");
		await Assert.That(cut.Find("[aria-label='Hour 00']").ClassList).Contains("is-inner");
	}

	[Test]
	public async Task MinuteDial_StepsByFiveMinutes()
	{
		var cut = Render<ClockDial>(parameters => parameters.Add(dial => dial.Kind, ClockDialKind.Minute).Add(dial => dial.Value, 30));

		await Assert
			.That(cut.FindAll(".clock-dial-mark").Select(mark => mark.TextContent))
			.IsEquivalentTo(["00", "05", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55"], CollectionOrdering.Matching);
		await Assert.That(cut.Find("[aria-pressed=true]").TextContent).IsEqualTo("30");
	}

	[Test]
	public async Task Mark_Tapped_PicksItsValue()
	{
		var picked = -1;
		var cut = Render<ClockDial>(parameters => parameters.Add(dial => dial.Kind, ClockDialKind.Hour).Add(dial => dial.ValueChanged, value => picked = value));

		await cut.Find("[aria-label='Hour 21']").ClickAsync(new());

		await Assert.That(picked).IsEqualTo(21);
	}
}
