using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Sections;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Shared.Layout;
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
	public async Task Remove_OnASinglePageListWithEveryItemSelected_AsksForConfirmationWithoutTheScopeSentence()
	{
		var removed = false;
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => removed = true);

		await ClickRemove(cut);

		var dialog = cut.Find("[role=alertdialog]");
		await Assert.That(removed).IsFalse();
		await Assert.That(dialog.TextContent).Contains("Remove 7 titles from your blocklist?");
		await Assert.That(cut.Find($"#{dialog.GetAttribute("aria-describedby")}").TextContent.Trim()).IsEqualTo("Body.");
	}

	[Test]
	public async Task Remove_WhenItAsks_MarksCancelAndConfirmForPhones()
	{
		var cut = RenderBar(selected: 7, page: 7, list: 7, onRemove: () => { });

		await ClickRemove(cut);

		var footer = cut.Find("[role=alertdialog] [data-dialog-actions]");
		await Assert.That(footer.QuerySelector("[data-dialog-cancel]")!.TextContent.Trim()).IsEqualTo("Cancel");
		await Assert.That(footer.QuerySelector("[data-dialog-commit]")!.TextContent).Contains("7");
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

	[Test]
	public async Task Phone_SelectBar_CountsSelectsTheWholeListAndLeaves()
	{
		var all = false;
		var left = false;
		OnAPhone();
		var cut = RenderBar(selected: 2, page: 20, list: 59, onRemove: () => { }, onSelectList: () => all = true, onExit: () => left = true);

		await Assert.That(cut.Find(".bar [role=status]").TextContent).IsEqualTo("2 selected");
		await cut.FindAll(".bar button").Single(button => button.TextContent.Trim() == "All 59").ClickAsync();
		await cut.Find(".bar [aria-label='Leave select mode']").ClickAsync();

		await Assert.That(all).IsTrue();
		await Assert.That(left).IsTrue();
	}

	[Test]
	public async Task Phone_Toolbar_CarriesMarkAndRemoveAsSlots()
	{
		OnAPhone();
		var cut = RenderBar(selected: 2, page: 20, list: 59, onRemove: () => { }, markable: 0);

		var slots = cut.FindAll(".bulk .toolbar-slot");
		await Assert.That(slots.Select(slot => slot.TextContent.Trim())).IsEquivalentTo(["Wantlist", "Remove"], CollectionOrdering.Matching);
		await Assert.That(slots[0].HasAttribute("disabled")).IsTrue();
		await Assert.That(slots[1].ClassList).Contains("is-danger");
	}

	[Test]
	public async Task Phone_RemovingPastTheLoadedTitles_StillAsksWithoutThePageSentence()
	{
		var removed = false;
		OnAPhone();
		var cut = RenderBar(selected: 58, page: 20, list: 59, onRemove: () => removed = true);

		await cut.Find(".bulk .toolbar-slot.is-danger").ClickAsync();

		await Assert.That(removed).IsFalse();
		await Assert.That(cut.Find("[role=alertdialog]").TextContent).DoesNotContain("on this page");
	}

	[Test]
	public async Task Phone_RemovingTheWholeList_AsksWithoutThePageSentence()
	{
		OnAPhone();
		var cut = RenderBar(selected: 59, page: 20, list: 59, onRemove: () => { });

		await cut.Find(".bulk .toolbar-slot.is-danger").ClickAsync();

		await Assert.That(cut.Find("[role=alertdialog]").TextContent).DoesNotContain("on this page");
	}

	private void OnAPhone()
	{
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, true));
	}

	private static Task ClickRemove(IRenderedComponent<Harness> cut) => cut.FindAll("button").First(button => button.TextContent.Trim() == "Remove").ClickAsync();

	private IRenderedComponent<Harness> RenderBar(
		int selected,
		int page,
		int list,
		Action onRemove,
		int? markable = null,
		int? removable = null,
		Action? onSelectList = null,
		Action? onExit = null
	)
	{
		return Render<Harness>(parameters =>
			parameters
				.Add(harness => harness.SelectedCount, selected)
				.Add(harness => harness.PageCount, page)
				.Add(harness => harness.ListCount, list)
				.Add(harness => harness.MarkableCount, markable ?? selected)
				.Add(harness => harness.RemovableCount, removable ?? selected)
				.Add(harness => harness.OnRemove, EventCallback.Factory.Create(this, onRemove))
				.Add(harness => harness.OnSelectList, EventCallback.Factory.Create(this, onSelectList ?? (() => { })))
				.Add(harness => harness.OnExit, EventCallback.Factory.Create(this, onExit ?? (() => { })))
		);
	}

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

		[Parameter]
		public EventCallback OnSelectList { get; set; }

		[Parameter]
		public EventCallback OnExit { get; set; }

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
			builder.AddAttribute(13, nameof(BulkActionBar.OnSelectList), OnSelectList);
			builder.AddAttribute(14, nameof(BulkActionBar.OnExit), OnExit);
			builder.CloseComponent();
			builder.AddContent(15, Outlet("bulk", PhoneLayout.BulkSection));
			builder.AddContent(16, Outlet("bar", PhoneLayout.BarSection));
		}

		private static RenderFragment Outlet(string name, string section)
		{
			return builder =>
			{
				builder.OpenElement(0, "output");
				builder.AddAttribute(1, "class", name);
				builder.OpenComponent<SectionOutlet>(2);
				builder.AddAttribute(3, nameof(SectionOutlet.SectionName), section);
				builder.CloseComponent();
				builder.CloseElement();
			};
		}
	}
}
