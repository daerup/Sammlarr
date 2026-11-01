using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;
using UI.Shared.Layout;

namespace UI.Tests.Common;

public class ListSearchTests : BunitContext
{
	private string? _changed = "untouched";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Desktop_ShowsTheField()
	{
		var cut = RenderSearch("da");

		await Assert.That(cut.Find("input[type=search]").GetAttribute("placeholder")).IsEqualTo("Search titles");
		await Assert.That(cut.FindAll(".list-search-chip")).IsEmpty();
	}

	[Test]
	public async Task Phone_WithoutASearch_ShowsNothing()
	{
		OnAPhone();

		var cut = RenderSearch(null);

		await Assert.That(cut.FindAll("input")).IsEmpty();
		await Assert.That(cut.FindAll("button")).IsEmpty();
	}

	[Test]
	public async Task Phone_WithASearch_ShowsAChipThatClearsIt()
	{
		OnAPhone();
		var cut = RenderSearch("da");
		await Assert.That(cut.FindAll("input")).IsEmpty();
		await Assert.That(cut.Find(".list-search-chip").TextContent.Trim()).IsEqualTo("“da”");

		await cut.Find(".list-search-chip").ClickAsync(new());

		await Assert.That(_changed).IsNull();
	}

	private void OnAPhone() =>
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, true));

	private IRenderedComponent<ListSearch> RenderSearch(string? value)
	{
		return Render<ListSearch>(parameters =>
			parameters
				.Add(search => search.Value, value)
				.Add(search => search.ValueChanged, changed => _changed = changed)
				.Add(search => search.Placeholder, "Search titles")
		);
	}
}
