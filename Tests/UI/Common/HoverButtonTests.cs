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
	public async Task Render_WithExtraClasses_AppendsThemAlongsideTheBaseClass()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Class, "mt-4").Add(button => button.Text, "Go"));

		var classes = cut.Find("button").ClassList;
		await Assert.That(classes).Contains("hover-button");
		await Assert.That(classes).Contains("mt-4");
	}

	[Test]
	public async Task Render_WithAnIconAndLabel_EmitsBoth()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Icon, "log-out").Add(button => button.Text, "Sign out"));

		await Assert.That(cut.Find("span").TextContent).IsEqualTo("Sign out");
		await Assert.That(cut.FindAll("svg").Count).IsGreaterThanOrEqualTo(1);
	}

	[Test]
	public async Task Click_WhenPressed_RaisesTheOnClickCallback()
	{
		var clicked = false;
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Text, "Go").Add(button => button.OnClick, () => clicked = true));

		await cut.Find("button").ClickAsync();

		await Assert.That(clicked).IsTrue();
	}

	[Test]
	public async Task Render_WhenDisabled_MarksTheButtonDisabled()
	{
		var cut = Render<HoverButton>(parameters => parameters.Add(button => button.Disabled, true).Add(button => button.Text, "Go"));

		await Assert.That(cut.Find("button").HasAttribute("disabled")).IsTrue();
	}
}
