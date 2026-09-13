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
	public async Task Render_ForAnEnum_EmitsOneButtonPerValue()
	{
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.First).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, _ => { })
		);

		await Assert.That(cut.FindAll("button").Count).IsEqualTo(3);
	}

	[Test]
	public async Task Click_WhenAnOptionIsChosen_RaisesOnChangeWithThatValue()
	{
		Choice? chosen = null;
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.First).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, value => chosen = value)
		);

		await cut.FindAll("button")[1].ClickAsync();

		await Assert.That(chosen).IsEqualTo(Choice.Second);
	}

	[Test]
	public async Task Render_WhenAnOptionIsSelected_MarksOnlyItPressed()
	{
		var cut = Render<EnumToggleGroup<Choice>>(parameters =>
			parameters.Add(group => group.Value, Choice.Second).Add(group => group.TooltipProvider, null).Add(group => group.OnChange, _ => { })
		);

		var buttons = cut.FindAll("button");
		await Assert.That(buttons[1].HasAttribute("aria-pressed")).IsTrue();
		await Assert.That(buttons[0].HasAttribute("aria-pressed")).IsFalse();
	}

	[Test]
	public async Task Click_WhenTheValueIsDisabled_DoesNotRaiseOnChange()
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
