using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class EnumToggleGroupTests : BunitContext
{
	private enum Choice
	{
		First,
		Second,
		Third,
	}

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task It_renders_one_button_per_enum_value()
	{
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.First).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, _ => { })
		);

		await Assert.That(cut.FindAll("button").Count).IsEqualTo(3);
	}

	[Test]
	public async Task Clicking_an_option_raises_on_change_with_that_value()
	{
		Choice? chosen = null;
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.First).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, value => chosen = value)
		);

		await cut.FindAll("button")[1].ClickAsync();

		await Assert.That(chosen).IsEqualTo(Choice.Second);
	}

	[Test]
	public async Task The_selected_option_is_pressed_and_the_others_are_not()
	{
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.Second).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, _ => { })
		);

		var buttons = cut.FindAll("button");
		await Assert.That(buttons[1].HasAttribute("aria-pressed")).IsTrue();
		await Assert.That(buttons[0].HasAttribute("aria-pressed")).IsFalse();
	}

	[Test]
	public async Task A_disabled_value_does_not_raise_on_change()
	{
		Choice? chosen = null;
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters
				.Add(group => group.Value, Choice.First)
				.Add(group => group.TooltipProvider, null)
				.Add(group => group.OnChange, value => chosen = value)
				.Add(group => group.DisabledValues, [Choice.Third])
		);

		await cut.FindAll("button")[2].ClickAsync();

		await Assert.That(chosen).IsNull();
	}
}
