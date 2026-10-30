using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class SectionCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Card_WithATitle_RendersTheBorderedCardWithItsHead()
	{
		var cut = Render<SectionCard>(parameters => parameters.Add(card => card.Icon, "tag").Add(card => card.Title, "Version").AddChildContent("<p>body</p>"));

		var root = cut.Find(".section-card");
		await Assert.That(root.ClassList).Contains("rounded-lg");
		await Assert.That(root.ClassList).DoesNotContain("w-fit");
		await Assert.That(root.TextContent).Contains("Version");
		await Assert.That(root.QuerySelector("p")!.TextContent).IsEqualTo("body");
	}

	[Test]
	public async Task Card_WithoutATitle_RendersNoHead()
	{
		var cut = Render<SectionCard>(parameters => parameters.Add(card => card.Class, "divide-y").AddChildContent("<p>row</p>"));

		var root = cut.Find(".section-card");
		await Assert.That(root.ClassList).Contains("divide-y");
		await Assert.That(root.Children.Length).IsEqualTo(1);
	}

	[Test]
	public async Task Fit_ShrinksTheCardToItsContent()
	{
		var cut = Render<SectionCard>(parameters =>
			parameters.Add(card => card.Look, SectionCardLook.Fit).Add(card => card.Icon, "network").Add(card => card.Title, "Which service does what")
		);

		await Assert.That(cut.Find(".section-card").ClassList).Contains("w-fit");
	}

	[Test]
	public async Task Large_RendersTheRoundedCardWithItsOwnHead()
	{
		var cut = Render<SectionCard>(parameters => parameters.Add(card => card.Look, SectionCardLook.Large).AddChildContent("<h4>own head</h4>"));

		var root = cut.Find(".section-card");
		await Assert.That(root.ClassList).Contains("rounded-xl");
		await Assert.That(root.TextContent.Trim()).IsEqualTo("own head");
	}

	[Test]
	public async Task Card_WithAMark_ShowsItInPlaceOfTheIcon()
	{
		RenderFragment mark = builder => builder.AddMarkupContent(0, "<span class=\"gold\">★</span>");

		var cut = Render<SectionCard>(parameters =>
			parameters.Add(card => card.Icon, "star").Add(card => card.Title, "Your Sammlarr rating").Add(card => card.Mark, mark)
		);

		await Assert.That(cut.Find(".section-card .gold").TextContent).IsEqualTo("★");
		await Assert.That(cut.FindAll(".section-card svg")).IsEmpty();
	}

	[Test]
	public async Task Panel_RendersTheTintedHeadWithTitleAndNote()
	{
		var cut = Render<SectionCard>(parameters =>
			parameters.Add(card => card.Look, SectionCardLook.Panel).Add(card => card.Title, "Up next").Add(card => card.Note, "nothing scheduled")
		);

		await Assert.That(cut.Find(".section-card").ClassList).Contains("panel");
		await Assert.That(cut.Find(".panel-title").TextContent).IsEqualTo("Up next");
		await Assert.That(cut.Find(".panel-note").TextContent).IsEqualTo("nothing scheduled");
	}

	[Test]
	public async Task Render_PassesUnmatchedAttributesToTheRoot()
	{
		var cut = Render<SectionCard>(parameters => parameters.AddUnmatched("data-you", true));

		await Assert.That(cut.Find(".section-card").HasAttribute("data-you")).IsTrue();
	}
}
