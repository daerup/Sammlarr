using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Sections;
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

	[Test]
	[Arguments(null, "Feeds")]
	[Arguments("Run history", "Run history")]
	public async Task PhoneTitle_FillsThePhoneBarWithTheHeadingUnlessSet(string? phoneTitle, string shown)
	{
		var cut = Render(builder =>
		{
			builder.OpenElement(0, "output");
			builder.OpenComponent<SectionOutlet>(1);
			builder.AddAttribute(2, nameof(SectionOutlet.SectionName), PhoneLayout.TitleSection);
			builder.CloseComponent();
			builder.CloseElement();
			builder.OpenComponent<PageContainer>(3);
			builder.AddAttribute(4, nameof(PageContainer.Heading), "Feeds");
			builder.AddAttribute(5, nameof(PageContainer.PhoneTitle), phoneTitle);
			builder.CloseComponent();
		});

		await Assert.That(cut.Find("output").TextContent).IsEqualTo(shown);
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task PhoneHeader_HidesTheHeaderBandOnPhonesOnlyWhenOff(bool phoneHeader)
	{
		var cut = Render<PageContainer>(parameters => parameters.Add(page => page.Heading, "Settings").Add(page => page.PhoneHeader, phoneHeader));

		await Assert.That(cut.FindAll(@"div.max-md\:hidden")).Count().IsEqualTo(phoneHeader ? 0 : 1);
	}
}
