using Bunit;
using TUnit.Assertions.Enums;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class ClockDialTests : BunitContext
{
	[Before(Test)]
	public void Setup() => JSInterop.Mode = JSRuntimeMode.Loose;

	[Test]
	public async Task HourDial_ShowsTwelveOutsideAndMidnightInside_AndMarksTheHourPicked()
	{
		var cut = Dial(ClockDialKind.Hour, new TimeSpan(18, 0, 0));

		var marks = cut.FindAll("[data-layer=hour] .clock-dial-mark");
		await Assert.That(marks).Count().IsEqualTo(24);
		await Assert
			.That(marks.Where(mark => !mark.ClassList.Contains("is-inner")).Select(mark => mark.TextContent))
			.IsEquivalentTo(Enumerable.Range(1, 12).Select(hour => hour.ToString()));
		await Assert.That(cut.Find("[aria-pressed=true]").GetAttribute("aria-label")).IsEqualTo("Hour 18");
		await Assert.That(cut.Find("[aria-label='Hour 00']").ClassList).Contains("is-inner");
		await Assert.That(cut.Find("[data-layer=minute]").HasAttribute("inert")).IsTrue();
	}

	[Test]
	public async Task MinuteDial_StepsByFiveMinutes_AndKeepsAnHourHandOnThePickedHour()
	{
		var cut = Dial(ClockDialKind.Minute, new TimeSpan(21, 30, 0));

		await Assert
			.That(cut.FindAll("[data-layer=minute] .clock-dial-mark").Select(mark => mark.TextContent))
			.IsEquivalentTo(["00", "05", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55"], CollectionOrdering.Matching);
		await Assert.That(cut.Find("[aria-pressed=true]").TextContent).IsEqualTo("30");
		await Assert.That(cut.Find(".clock-dial").ClassList).Contains("is-minute");
		await Assert.That(cut.Find(".clock-dial-hour-hand").GetAttribute("style")).IsEqualTo("transform: rotate(285deg)");
	}

	[Test]
	public async Task Mark_Tapped_PicksItsValue()
	{
		var picked = -1;
		var cut = Render<ClockDial>(parameters => parameters.Add(dial => dial.Kind, ClockDialKind.Hour).Add(dial => dial.ValueChanged, value => picked = value));

		await cut.Find("[aria-label='Hour 21']").ClickAsync(new());

		await Assert.That(picked).IsEqualTo(21);
	}

	[Test]
	public async Task Hand_FromElevenToOne_TurnsTheShortWayRound()
	{
		var cut = Dial(ClockDialKind.Hour, new TimeSpan(11, 0, 0));

		cut.Render(parameters => parameters.Add(dial => dial.Time, new TimeSpan(1, 0, 0)));

		await Assert.That(cut.Find(".clock-dial-cursor").GetAttribute("style")).IsEqualTo("transform: rotate(390deg)");
	}

	[Test]
	public async Task Drag_ToAnExactMinute_PicksItAndShowsItOnTheTip()
	{
		var picked = -1;
		var cut = Render<ClockDial>(parameters =>
			parameters
				.Add(dial => dial.Kind, ClockDialKind.Minute)
				.Add(dial => dial.Time, new TimeSpan(21, 45, 0))
				.Add(dial => dial.ValueChanged, value => picked = value)
		);

		await cut.InvokeAsync(() => cut.Instance.DragTo(47));
		cut.Render(parameters => parameters.Add(dial => dial.Time, new TimeSpan(21, 47, 0)));

		await Assert.That(picked).IsEqualTo(47);
		await Assert.That(cut.Find(".clock-dial-exact").TextContent).IsEqualTo("47");
		await Assert.That(cut.FindAll("[aria-pressed=true]")).IsEmpty();
	}

	private IRenderedComponent<ClockDial> Dial(ClockDialKind kind, TimeSpan time) =>
		Render<ClockDial>(parameters => parameters.Add(dial => dial.Kind, kind).Add(dial => dial.Time, time));
}
