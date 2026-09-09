using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class HoverButtonTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Every_hover_button_carries_the_base_class()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").ClassList).Contains("hover-button");
	}

	[Test]
	[Arguments(HoverButtonPreset.Custom, "hover-button--custom")]
	[Arguments(HoverButtonPreset.Destructive, "hover-button--destructive")]
	[Arguments(HoverButtonPreset.Success, "hover-button--success")]
	public async Task The_preset_selects_its_modifier_class(HoverButtonPreset preset, string modifier)
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Preset, preset).Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").ClassList).Contains(modifier);
	}

	[Test]
	public async Task A_custom_color_is_wired_through_the_tint_variable()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Color, "#3b82f6").Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").GetAttribute("style")).Contains("--tint: #3b82f6");
	}

	[Test]
	public async Task Without_a_custom_color_the_button_leaves_the_tint_to_the_theme()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Preset, HoverButtonPreset.Destructive).Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").GetAttribute("style") ?? string.Empty).DoesNotContain("--tint");
	}

	[Test]
	public async Task A_borderless_button_gains_the_modifier_and_a_bordered_one_does_not()
	{
		var borderless = Render<HoverButton>(parameters => parameters.Add(button => button.Border, false).Add(button => button.Text, "Go"));
		var bordered = Render<HoverButton>(parameters => parameters.Add(button => button.Text, "Go"));

		await Assert.That(borderless.Find("button").ClassList).Contains("hover-button--borderless");
		await Assert.That(bordered.Find("button").ClassList).DoesNotContain("hover-button--borderless");
	}

	[Test]
	public async Task Extra_classes_are_appended_alongside_the_base_class()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Class, "mt-4").Add(button => button.Text, "Go"));

		var classes = cut.Find("button").ClassList;
		await Assert.That(classes).Contains("hover-button");
		await Assert.That(classes).Contains("mt-4");
	}

	[Test]
	public async Task It_renders_the_icon_and_the_label()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Icon, "log-out").Add(button => button.Text, "Sign out"));

		await Assert.That(cut.Find("span").TextContent).IsEqualTo("Sign out");
		await Assert.That(cut.FindAll("svg").Count).IsGreaterThanOrEqualTo(1);
	}

	[Test]
	public async Task Clicking_raises_the_on_click_callback()
	{
		var clicked = false;
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Text, "Go").Add(button => button.OnClick, () => clicked = true));

		await cut.Find("button").ClickAsync();

		await Assert.That(clicked).IsTrue();
	}

	[Test]
	public async Task A_disabled_button_is_marked_disabled()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Disabled, true).Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").HasAttribute("disabled")).IsTrue();
	}
}
