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
		var cut = Render(BesideOutlet(PhoneLayout.TitleSection, nameof(PageContainer.PhoneTitle), phoneTitle));

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

	[Test]
	[Arguments(true, 1)]
	[Arguments(false, 0)]
	public async Task Actions_FillTheToolbarOnlyOnAPhone(bool phone, int slots)
	{
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, phone));
		RenderFragment actions = builder =>
		{
			builder.OpenComponent<PageAction>(0);
			builder.AddAttribute(1, nameof(PageAction.Icon), "play");
			builder.AddAttribute(2, nameof(PageAction.Label), "Run");
			builder.AddAttribute(3, nameof(PageAction.OnClick), EventCallback.Factory.Create(this, () => { }));
			builder.CloseComponent();
		};

		var cut = Render(BesideOutlet(PhoneLayout.ToolbarSection, nameof(PageContainer.Actions), actions));

		await Assert.That(cut.Find("output").QuerySelectorAll(".toolbar-slot").Length).IsEqualTo(slots);
		await Assert.That(cut.FindAll(@"div.max-md\:hidden > button")).HasSingleItem();
	}

	private static RenderFragment BesideOutlet(string section, string parameter, object? value)
	{
		return builder =>
		{
			builder.OpenElement(0, "output");
			builder.OpenComponent<SectionOutlet>(1);
			builder.AddAttribute(2, nameof(SectionOutlet.SectionName), section);
			builder.CloseComponent();
			builder.CloseElement();
			builder.OpenComponent<PageContainer>(3);
			builder.AddAttribute(4, nameof(PageContainer.Heading), "Feeds");
			builder.AddAttribute(5, parameter, value);
			builder.CloseComponent();
		};
	}
}
