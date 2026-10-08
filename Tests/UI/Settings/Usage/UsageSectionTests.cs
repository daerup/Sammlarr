using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageSectionTests : BunitContext
{
	private readonly Mock<IUsageReport> _report = IUsageReport.Mock();

	[Before(Test)]
	public void Setup()
	{
		_report.ReadAsync(Any(), Any()).Returns(UsageSamples.Snapshot());
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(UsageSamples.Clock());
		Services.AddSingleton(_report.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_OnceTheReportIsRead_ShowsTheCardsForTheLastDay()
	{
		var cut = Render<UsageSection>();

		cut.WaitForElement("[data-testid=usage-calls]");

		await Assert.That(cut.FindAll("[data-testid=usage-stat-value]").Select(value => value.TextContent)).IsEquivalentTo(["140", "2.1%", "100 ms", "2.9%"]);
		_report.ReadAsync(UsageRange.Day, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Render_ForAnInstallThatCalledNothing_SaysNoCallsYet()
	{
		_report.ReadAsync(Any(), Any()).Returns(new UsageSnapshot(UsageSamples.Period([], []), UsageSamples.Period([], []), []));

		var cut = Render<UsageSection>();

		var empty = cut.WaitForElement("[data-testid=usage-empty]");
		await Assert.That(empty.TextContent).Contains("No calls yet");
		await Assert.That(cut.FindAll("[data-testid=usage-calls]")).IsEmpty();
	}

	[Test]
	public async Task Range_SwitchedToSevenDays_AsksForThatWindow()
	{
		var cut = Render<UsageSection>();
		cut.WaitForElement("[data-testid=usage-calls]");

		await cut.FindAll("[role=radio], button").First(button => button.TextContent.Trim() == "7d").ClickAsync();

		cut.WaitForElement("[data-testid=usage-calls]");
		_report.ReadAsync(UsageRange.Week, Any()).WasCalled(Times.Once);
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All services: last 7 days, each day split by how its calls ended");
	}
}
