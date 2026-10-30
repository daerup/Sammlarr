using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using TUnit.Assertions.Enums;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class PageContainerTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Header_WithSearchAndActions_PutsTheSearchFirst()
	{
		RenderFragment search = builder => builder.AddMarkupContent(0, "<input class=\"search\"/>");
		RenderFragment actions = builder => builder.AddMarkupContent(0, "<button class=\"action\">Run feeds</button>");

		var cut = Render<PageContainer>(parameters =>
			parameters.Add(page => page.Heading, "Feeds").Add(page => page.HeaderSearch, search).Add(page => page.Actions, actions)
		);

		var row = cut.Find(".search").ParentElement!;
		await Assert.That(row.Children.Select(child => child.TagName)).IsEquivalentTo(["INPUT", "BUTTON"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Header_WithoutSearchOrActions_RendersNoActionRow()
	{
		var cut = Render<PageContainer>(parameters => parameters.Add(page => page.Heading, "Browse"));

		await Assert.That(cut.FindAll("h1")).HasSingleItem();
		await Assert.That(cut.FindAll(".shrink-0.gap-2")).IsEmpty();
	}
}
