using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Filtering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Pages.Editor.Filters;
using UI.Tests.Media;

namespace UI.Tests.Feeds.Filters;

public class FeedFilterGroupTests : BunitContext
{
	private const string Operator = "[role=combobox][aria-label='Conditions to match']";

	private int _changes;

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(FeedFilterStub.Shipped);
		Services.AddProviderBranding();
		Services.AddSingleton(RatingStub.Context());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForAGroupOfTwo_LabelsItAboveTheCardInWords()
	{
		var cut = RenderGroup(new FilterDefinition { Operator = LogicalOperator.Or, Conditions = [FeedFields.NewCondition(), FeedFields.NewCondition()] });

		await Assert.That(cut.Find(Operator).TextContent.Trim()).IsEqualTo("any of");
	}

	[Test]
	public async Task Render_ForAGroupOfOne_ShowsNoLabel()
	{
		var cut = RenderGroup(new FilterDefinition { Conditions = [FeedFields.NewCondition()] });

		await Assert.That(cut.FindAll(Operator)).IsEmpty();
	}

	[Test]
	public async Task Render_ForANestedGroupOfOne_ShowsNoLabelAndRemovesBesideTheAddButtons()
	{
		var cut = RenderGroup(new FilterDefinition { Conditions = [FeedFields.NewCondition()] }, depth: 1);

		await Assert.That(cut.FindAll(Operator)).IsEmpty();
		await Assert.That(cut.Find("[aria-label='Remove group']").ParentElement!.TextContent).Contains("Add condition");
	}

	[Test]
	public async Task Operator_WhenAnyOfIsPicked_SetsItAndRaisesTheChange()
	{
		var group = new FilterDefinition { Conditions = [FeedFields.NewCondition(), FeedFields.NewCondition()] };
		var cut = RenderGroup(group);

		await cut.Find(Operator).ClickAsync(new());
		await cut.FindAll("[role=option]").Single(option => option.TextContent.Trim() == "any of").ClickAsync(new());

		await Assert.That(group.Operator).IsEqualTo(LogicalOperator.Or);
		await Assert.That(_changes).IsEqualTo(1);
	}

	[Test]
	public async Task Operator_WhenOpened_SizesTheListToItsWordsNotToTheTrigger()
	{
		var cut = RenderGroup(new FilterDefinition { Conditions = [FeedFields.NewCondition(), FeedFields.NewCondition()] });

		await cut.Find(Operator).ClickAsync(new());

		await Assert.That(cut.Find("[role=listbox]").ClassList).Contains("min-w-max");
	}

	[Test]
	public async Task AddGroup_PastTheConfiguredLevels_IsNoLongerOffered()
	{
		Services.AddSingleton(FeedFilterStub.Shipped with { Levels = [.. FeedFilterStub.Shipped.Levels.Take(2)] });
		var nested = new FilterDefinition { Conditions = [FeedFields.NewCondition()] };

		var cut = RenderGroup(new FilterDefinition { Conditions = [FeedFields.NewCondition()], Groups = [nested] });

		await Assert.That(cut.FindAll("button").Where(button => button.TextContent.Trim() == "Add group")).HasSingleItem();
	}

	private IRenderedComponent<IComponent> RenderGroup(FilterDefinition group, int depth = 0)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedFilterGroup>(1);
			builder.AddComponentParameter(2, nameof(FeedFilterGroup.Group), group);
			builder.AddComponentParameter(3, nameof(FeedFilterGroup.Depth), depth);
			builder.AddComponentParameter(4, nameof(FeedFilterGroup.Changed), EventCallback.Factory.Create(this, () => _changes++));
			builder.AddComponentParameter(5, nameof(FeedFilterGroup.Adornments), new Dictionary<string, RenderFragment>());
			builder.CloseComponent();
		});
	}
}
