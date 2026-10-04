using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Testing;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class TestingSectionTests : BunitContext
{
	private const string Demos = "UI.Features.Settings.Components.Testing.Demos";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<ScrollService>();
		ComponentFactories.AddStub(type => type.Namespace == Demos);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Section_HeadsEachGroupInTheJumpRowsOrder()
	{
		var cut = Render<TestingSection>();

		var headings = cut.FindAll("[role=region] > h3").Select(heading => heading.TextContent.Trim());
		var jumps = cut.FindAll("nav[aria-label='Jump to a group'] button").Select(button => button.TextContent.Trim());
		await Assert.That(headings).IsEquivalentTo(TestingGroup.All.Select(group => group.Name), CollectionOrdering.Matching);
		await Assert.That(jumps).IsEquivalentTo(TestingGroup.All.Select(group => group.Name), CollectionOrdering.Matching);
	}

	[Test]
	public async Task JumpRow_WhenAGroupIsPicked_ScrollsToItsHeading()
	{
		var cut = Render<TestingSection>();

		await cut.FindAll("nav[aria-label='Jump to a group'] button").Single(button => button.TextContent.Trim() == "Branding").ClickAsync();

		await Assert.That(JSInterop.VerifyInvoke("sammlarrScroll.intoView").Arguments[0]).IsEqualTo("testing-branding");
		await Assert.That(cut.Find("#testing-branding > h3").TextContent.Trim()).IsEqualTo("Branding");
	}

	[Test]
	public async Task Section_HoldsEveryDemo()
	{
		var cut = Render<TestingSection>();

		var shown = cut.FindComponents<IComponent>().Select(component => component.Instance.GetType()).Where(IsStub).ToHashSet();
		var demos = typeof(TestingGroup).Assembly.GetTypes().Where(type => type.Namespace == Demos && typeof(IComponent).IsAssignableFrom(type));
		await Assert.That(shown.Count).IsEqualTo(demos.Count());
	}

	private static bool IsStub(Type type) => type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("Stub", StringComparison.Ordinal);
}
