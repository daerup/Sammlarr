using AngleSharp.Dom;
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

	private readonly List<Choice> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForAnEnum_EmitsOneRadioPerValue()
	{
		var cut = RenderGroup(Choice.First);

		await Assert.That(Radios(cut).Count).IsEqualTo(3);
		await Assert.That(Radios(cut).All(radio => radio.QuerySelector("span[id]") is null)).IsTrue();
		await Assert.That(cut.Find("[role=radiogroup]").GetAttribute("aria-label")).IsEqualTo("Enum selection");
	}

	[Test]
	public async Task Render_WhenAValueIsChosen_ChecksOnlyIt()
	{
		var cut = RenderGroup(Choice.Second);

		await Assert.That(string.Join(",", Radios(cut).Select(radio => radio.GetAttribute("aria-checked")))).IsEqualTo("false,true,false");
	}

	[Test]
	public async Task Click_OnAnotherValue_RaisesOnChangeWithIt()
	{
		var cut = RenderGroup(Choice.First);

		await Radios(cut)[1].ClickAsync();

		await Assert.That(_changes).IsEquivalentTo([Choice.Second]);
	}

	[Test]
	public async Task Click_OnTheChosenValue_RaisesNothing()
	{
		var cut = RenderGroup(Choice.First);

		await Radios(cut)[0].ClickAsync();

		await Assert.That(_changes).IsEmpty();
	}

	[Test]
	public async Task Click_OnADisabledValue_RaisesNothing()
	{
		var cut = RenderGroup(Choice.First, parameters => parameters.Add(group => group.DisabledValues, [Choice.Third]));

		await Assert.That(Radios(cut)[2].HasAttribute("disabled")).IsTrue();
		await Radios(cut)[2].ClickAsync();

		await Assert.That(_changes).IsEmpty();
	}

	[Test]
	public async Task Render_WhenDisabled_DisablesEveryValue()
	{
		var cut = RenderGroup(Choice.First, parameters => parameters.Add(group => group.Disabled, true));

		await Assert.That(Radios(cut).All(radio => radio.HasAttribute("disabled"))).IsTrue();
	}

	[Test]
	public async Task Render_WithATooltipProvider_KeepsEachValueOneRadio()
	{
		var cut = RenderGroup(Choice.First, parameters => parameters.Add(group => group.TooltipProvider, choice => $"About {choice}"));

		var radios = Radios(cut);
		await Assert.That(radios.Count).IsEqualTo(3);
		await Assert.That(cut.FindAll("[role=radiogroup] [tabindex='0']").Count).IsEqualTo(1);
		await Assert.That(radios[1].TextContent.Trim()).IsEqualTo(nameof(Choice.Second));
		await Assert.That(radios.All(radio => radio.QuerySelector("span[id]") is not null)).IsTrue();
	}

	private IRenderedComponent<EnumToggleGroup<Choice>> RenderGroup(
		Choice value,
		Action<ComponentParameterCollectionBuilder<EnumToggleGroup<Choice>>>? configure = null
	)
	{
		return Render<EnumToggleGroup<Choice>>(parameters =>
		{
			parameters.Add(group => group.Value, value).Add(group => group.OnChange, _changes.Add);
			configure?.Invoke(parameters);
		});
	}

	private static IReadOnlyList<IElement> Radios(IRenderedComponent<EnumToggleGroup<Choice>> cut) => cut.FindAll("[role=radio]");
}
