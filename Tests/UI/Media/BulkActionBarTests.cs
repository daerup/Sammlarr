using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class BulkActionBarTests : BunitContext
{
	private static readonly Func<int, BulkConfirm> Confirm = count => new($"Remove {count} titles from your blocklist?", "Body.");

	private static readonly Func<int, BulkConfirm> MarkCopy = count => new($"Wantlist {count} titles?", "They stay blocklisted.");

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Remove_WhenEverySinglePageItemIsSelected_AsksForConfirmationEvenWithoutAListScopeEntry()
	{
		var removed = false;
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => removed = true);

		await ClickRemove(cut);

		await Assert.That(removed).IsFalse();
		await Assert.That(cut.Markup).Contains("Remove 7 titles from your blocklist?");
	}

	[Test]
	public async Task Remove_WhenOnlyThePageIsSelectedOutOfALongerList_RunsWithoutConfirmation()
	{
		var removed = false;
		var cut = RenderBar(selected: 48, page: 48, list: 1614, onRemove: () => removed = true);

		await ClickRemove(cut);

		await Assert.That(removed).IsTrue();
	}

	[Test]
	public async Task Remove_WhenMoreThanOnePageWorthIsSelected_AsksForConfirmation()
	{
		var removed = false;
		var cut = RenderBar(selected: 1612, page: 48, list: 1614, onRemove: () => removed = true);

		await ClickRemove(cut);

		await Assert.That(removed).IsFalse();
		await Assert.That(cut.Markup).Contains("not just the 48 on this page");
	}

	[Test]
	public async Task Confirm_OnASinglePageList_LeavesOutTheScopeSentenceBecauseThereIsNoOtherPage()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { });

		await ClickRemove(cut);

		await Assert.That(cut.Markup).DoesNotContain("on this page.");
	}

	[Test]
	public async Task Cancel_AfterArmingTheConfirmation_ClosesItWithoutRunningTheAction()
	{
		var removed = false;
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => removed = true);
		await ClickRemove(cut);

		var cancel = cut.FindAll("button").First(button => button.TextContent.Trim() == "Cancel");
		await cancel.ClickAsync();

		await Assert.That(removed).IsFalse();
		await Assert.That(cut.Markup).DoesNotContain("Remove 7 titles from your blocklist?");
	}

	[Test]
	public async Task Remove_WhenEverySelectedTitleIsAlreadyPendingRemoval_IsDisabledAndSaysWhy()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { }, removable: 0);

		var remove = cut.FindAll("button").First(button => button.TextContent.Trim() == "Remove");

		await Assert.That(remove.HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Every selected title is already pending removal.");
	}

	[Test]
	public async Task Remove_WhenSomeAreAlreadyPendingRemoval_CountsOnlyTheOnesThatWouldChange()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { }, removable: 2);

		await ClickRemove(cut);

		await Assert.That(cut.Markup).Contains("Remove 2 titles from your blocklist?");
	}

	[Test]
	public async Task Mark_WhenEverySelectedTitleIsAlreadyOnTheOtherList_IsDisabledAndSaysWhy()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { }, markable: 0);

		var mark = cut.FindAll("button").First(button => button.TextContent.Trim() == "Wantlist");

		await Assert.That(mark.HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Every selected title is already on your wantlist.");
	}

	[Test]
	public async Task Mark_WhenSomeAreAlreadyOnTheOtherList_CountsOnlyTheOnesThatWouldChange()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { }, markable: 1);

		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Wantlist").ClickAsync();

		await Assert.That(cut.Markup).Contains("Wantlist 1");
		await Assert.That(cut.Markup).DoesNotContain("Wantlist 7 title");
	}

	private static Task ClickRemove(IRenderedComponent<Harness> cut) => cut.FindAll("button").First(button => button.TextContent.Trim() == "Remove").ClickAsync();

	private IRenderedComponent<Harness> RenderBar(int selected, int page, int list, Action onRemove, int? markable = null, int? removable = null) =>
		Render<Harness>(parameters =>
			parameters
				.Add(harness => harness.SelectedCount, selected)
				.Add(harness => harness.PageCount, page)
				.Add(harness => harness.ListCount, list)
				.Add(harness => harness.MarkableCount, markable ?? selected)
				.Add(harness => harness.RemovableCount, removable ?? selected)
				.Add(harness => harness.OnRemove, EventCallback.Factory.Create(this, onRemove))
		);

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public int SelectedCount { get; set; }

		[Parameter]
		public int PageCount { get; set; }

		[Parameter]
		public int ListCount { get; set; }

		[Parameter]
		public EventCallback OnRemove { get; set; }

		[Parameter]
		public int MarkableCount { get; set; }

		[Parameter]
		public int RemovableCount { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<BulkActionBar>(1);
			builder.AddAttribute(2, nameof(BulkActionBar.SelectedCount), SelectedCount);
			builder.AddAttribute(3, nameof(BulkActionBar.PageCount), PageCount);
			builder.AddAttribute(4, nameof(BulkActionBar.ListCount), ListCount);
			builder.AddAttribute(5, nameof(BulkActionBar.OnRemove), OnRemove);
			builder.AddAttribute(6, nameof(BulkActionBar.RemoveConfirm), Confirm);
			builder.AddAttribute(7, nameof(BulkActionBar.MarkLabel), "Wantlist");
			builder.AddAttribute(8, nameof(BulkActionBar.MarkableCount), MarkableCount);
			builder.AddAttribute(9, nameof(BulkActionBar.AlreadyMarkedHint), "Every selected title is already on your wantlist.");
			builder.AddAttribute(10, nameof(BulkActionBar.MarkConfirm), MarkCopy);
			builder.AddAttribute(11, nameof(BulkActionBar.RemovableCount), RemovableCount);
			builder.AddAttribute(12, nameof(BulkActionBar.AlreadyRemovedHint), "Every selected title is already pending removal.");
			builder.CloseComponent();
		}
	}
}
