using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Processing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedDateRuleEditorTests : BunitContext
{
	private static readonly DateOnly Today = new(2026, 10, 18);

	private readonly List<object?> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(2, DateUnit.Years, false, "2 years ago")]
	[Arguments(1, DateUnit.Weeks, false, "1 week ago")]
	[Arguments(30, DateUnit.Days, true, "30 days from now")]
	[Arguments(0, DateUnit.Days, false, "today")]
	public async Task Trigger_ForARelativeDate_ReadsAsWords(int amount, DateUnit unit, bool ahead, string expected)
	{
		var cut = RenderEditor(new RelativeDate(amount, unit, ahead));

		await Assert.That(Shown(cut)).IsEqualTo(expected);
	}

	[Test]
	public async Task Trigger_ForAFixedDate_ShowsTheDay()
	{
		var cut = RenderEditor(new DateTime(1980, 1, 1));

		await Assert.That(Shown(cut)).IsEqualTo("1 Jan 1980");
	}

	[Test]
	public async Task RelativeTab_WhenOpenedOnAFixedDate_SwitchesToOneYearAgo()
	{
		var cut = await OpenAsync(new DateTime(1980, 1, 1));

		await Tab(cut, "Relative").ClickAsync(new());

		await Assert.That(_changes.Last()).IsEqualTo(new RelativeDate(1, DateUnit.Years, Ahead: false));
	}

	[Test]
	public async Task DateTab_AfterPeekingAtRelative_BringsTheFixedDateBack()
	{
		var picked = new DateTime(1980, 1, 1);
		var cut = await OpenAsync(picked);
		await Tab(cut, "Relative").ClickAsync(new());
		Rerender(cut, _changes.Last());

		await Tab(cut, "Date").ClickAsync(new());

		await Assert.That(_changes.Last()).IsEqualTo(picked);
	}

	[Test]
	public async Task RelativeTab_ForTwoYearsAgo_SaysWhichDayTodaysRunCompares()
	{
		var cut = await OpenAsync(new RelativeDate(2, DateUnit.Years, Ahead: false));

		await Assert.That(cut.Markup).Contains("On today's run: 18 Oct 2024");
	}

	[Test]
	public async Task Unit_WhenMonthsArePicked_KeepsTheAmount()
	{
		var cut = await OpenAsync(new RelativeDate(6, DateUnit.Years, Ahead: false));

		await cut.FindAll("[role=dialog] [role=combobox]").Single().ClickAsync(new());
		await cut.FindAll("[role=option]").Single(option => option.TextContent.Trim() == "months").ClickAsync(new());

		await Assert.That(_changes.Last()).IsEqualTo(new RelativeDate(6, DateUnit.Months, Ahead: false));
	}

	[Test]
	public async Task Direction_WhenFromNowIsPicked_LooksAhead()
	{
		var cut = await OpenAsync(new RelativeDate(30, DateUnit.Days, Ahead: false));

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "from now").ClickAsync(new());

		await Assert.That(_changes.Last()).IsEqualTo(new RelativeDate(30, DateUnit.Days, Ahead: true));
	}

	private static string Shown(IRenderedComponent<IComponent> cut) => cut.Find("[data-date-rule]").TextContent.Trim();

	private static IElement Tab(IRenderedComponent<IComponent> cut, string name) => cut.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == name);

	private async Task<IRenderedComponent<IComponent>> OpenAsync(object value)
	{
		var cut = RenderEditor(value);
		await cut.Find("button[aria-label='Release Date']").ClickAsync(new());
		return cut;
	}

	private static void Rerender(IRenderedComponent<IComponent> cut, object? value)
	{
		cut.FindComponent<FeedDateRuleEditor>().Render(parameters => parameters.Add(editor => editor.Value, value));
	}

	private IRenderedComponent<IComponent> RenderEditor(object value)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedDateRuleEditor>(1);
			builder.AddComponentParameter(2, nameof(FeedDateRuleEditor.Value), value);
			builder.AddComponentParameter(3, nameof(FeedDateRuleEditor.ValueChanged), EventCallback.Factory.Create<object?>(this, _changes.Add));
			builder.AddComponentParameter(4, nameof(FeedDateRuleEditor.Label), "Release Date");
			builder.CloseComponent();
		});
	}
}
