using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Sections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Observability.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Diagnostics;
using UI.Shared.Layout;

namespace UI.Tests.Settings;

public class DiagnosticsSectionTests : BunitContext
{
	private readonly LogCaptureService _capture = new(new LoggingLevelSwitch());

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(_capture);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Phone_BeforeCapturing_OffersStartAndNoPause()
	{
		var cut = RenderSection(phone: true);

		await Assert.That(Slots(cut)).IsEquivalentTo(["Start", "Clear", "Export", "Options"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Phone_WhileCapturing_OffersStopAndPauseThenResume()
	{
		_capture.Start(LogLevel.Information);
		var cut = RenderSection(phone: true);

		await Assert.That(Slots(cut)).IsEquivalentTo(["Stop", "Pause", "Clear", "Export", "Options"], CollectionOrdering.Matching);
		await Slot(cut, "Pause").ClickAsync();

		await Assert.That(_capture.IsPaused).IsTrue();
		await Assert.That(Slots(cut)).Contains("Resume");
	}

	[Test]
	public async Task Options_OnAPhone_OpensASheetWithTheVerbosityAndTheBufferSize()
	{
		var cut = RenderSection(phone: true);

		await Slot(cut, "Options").ClickAsync();

		var dialog = cut.WaitForElement("[role=dialog]");
		await Assert.That(dialog.TextContent).Contains("Verbosity").And.Contains("Buffer size");
		await Assert.That(dialog.QuerySelectorAll("[role=combobox]").Length).IsEqualTo(2);
	}

	[Test]
	public async Task Entry_OnAPhone_PutsTheCategoryUnderTheTimeAndOpensItsExceptionInASheet()
	{
		this.MeasureVirtualizeByHand();
		_capture.Start(LogLevel.Information);
		new InMemorySink(_capture).Emit(
			new LogEvent(
				DateTimeOffset.Now,
				LogEventLevel.Error,
				new InvalidOperationException("boom"),
				new MessageTemplateParser().Parse("Feed run failed"),
				[new LogEventProperty("SourceContext", new ScalarValue("Core.Feeds.FeedRunner"))]
			)
		);
		var cut = RenderSection(phone: true);
		cut.WaitForState(() => JSInterop.Invocations["Blazor._internal.Virtualize.init"].Count > 0);
		await this.ReportViewportAsync(cut);

		var entry = cut.WaitForElement("[data-log-entry]");
		await Assert.That(entry.Children[1].TextContent).IsEqualTo("Core.Feeds.FeedRunner");
		await Assert.That(cut.Markup).DoesNotContain("InvalidOperationException: boom");
		await cut.FindAll("[data-log-entry] button").Single(button => button.TextContent.Contains("show the exception", StringComparison.Ordinal)).ClickAsync();

		cut.WaitForState(() => cut.Markup.Contains("InvalidOperationException: boom", StringComparison.Ordinal));
		await Assert.That(cut.Markup).Contains("aria-modal=\"true\"");
	}

	[Test]
	public async Task Desktop_KeepsItsControlsRowAndNoToolbar()
	{
		var cut = RenderSection(phone: false);

		await Assert.That(cut.FindAll(".toolbar-slot")).IsEmpty();
		await Assert.That(cut.FindAll("[role=combobox]").Count).IsEqualTo(2);
		await Assert.That(cut.FindAll("button").Select(button => button.TextContent.Trim())).Contains("Start");
	}

	private IRenderedComponent<IComponent> RenderSection(bool phone)
	{
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, phone));
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<SectionOutlet>(1);
			builder.AddAttribute(2, nameof(SectionOutlet.SectionName), PhoneLayout.ToolbarSection);
			builder.CloseComponent();
			builder.OpenComponent<DiagnosticsSection>(3);
			builder.CloseComponent();
		});
	}

	private static IReadOnlyList<string> Slots(IRenderedComponent<IComponent> cut) =>
		[.. cut.FindAll(".toolbar-slot .toolbar-slot-label").Select(label => label.TextContent.Trim())];

	private static IElement Slot(IRenderedComponent<IComponent> cut, string label) =>
		cut.FindAll(".toolbar-slot").Single(slot => slot.QuerySelector(".toolbar-slot-label")!.TextContent.Trim() == label);
}
