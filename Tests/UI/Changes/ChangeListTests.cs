using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Changes;
using UI.Tests.Media;

namespace UI.Tests.Changes;

public class ChangeListTests : BunitContext
{
	[Test]
	public async Task Render_ForAMixedChangeset_PutsRemovalsBeforeAdditions()
	{
		var cut = RenderList(Removed("Gone"), Added("Fresh"), Added("Also Fresh"));

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups[0]).Contains("Removed");
		await Assert.That(groups[1]).Contains("Added");
	}

	[Test]
	public async Task Render_ForEveryRow_ShowsTheContextAsAnIconWithoutWritingItOut()
	{
		var item = Removed("Gone") with { ContextIcon = "log-out", ContextTooltip = "Absent from 3 of 3 runs" };

		var cut = RenderList(item);

		await Assert.That(cut.FindAll(".change-context")).Count().IsEqualTo(1);
		await Assert.That(cut.Find(".change-row").TextContent).DoesNotContain("Absent from");
	}

	[Test]
	public async Task Render_ForATitleNoSinkCouldAdd_ListsItUnderItsOwnGroupAfterTheAdded()
	{
		var cut = RenderList(Added("Fresh"), new ChangeItem { Title = "Stuck", Kind = ChangeKind.Unaddressable });

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();
		await Assert.That(groups).Count().IsEqualTo(2);
		await Assert.That(groups[1]).Contains("Couldn't be added");
	}

	[Test]
	public async Task Render_ForAnUnmatchedRow_UsesAPlaceholderInsteadOfAPoster()
	{
		var cut = RenderList(new ChangeItem { Title = "Nowhere", Kind = ChangeKind.Unmatched });

		await Assert.That(cut.FindAll(".change-poster.is-missing")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll("img.change-poster")).IsEmpty();
	}

	[Test]
	public async Task Chips_AfterTheSeparator_AreReadoutsRatherThanButtons()
	{
		var cut = RenderList(
			[Added("Fresh")],
			[
				new ChangeChip
				{
					Icon = "plus",
					Count = 1,
					Tooltip = "Added",
					Tone = ChipTone.Added,
					Filters = ChangeKind.Added,
				},
				new ChangeChip
				{
					Icon = "bookmark-check",
					Count = 178,
					Tooltip = "Already yours",
					StartsCountsOnly = true,
				},
			]
		);

		var chips = cut.FindAll(".change-chip");

		await Assert.That(chips[0].TagName).IsEqualTo("BUTTON");
		await Assert.That(chips[1].TagName).IsNotEqualTo("BUTTON");
		await Assert.That(chips[1].ClassList).Contains("is-count");
		await Assert.That(cut.FindAll(".change-chip-sep")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Chips_AfterTheSeparator_StayInertOnceAFilterIsPicked()
	{
		var cut = RenderList(
			[Added("Fresh"), Removed("Gone")],
			[
				new ChangeChip
				{
					Icon = "plus",
					Count = 1,
					Tooltip = "Added",
					Tone = ChipTone.Added,
					Filters = ChangeKind.Added,
				},
				new ChangeChip
				{
					Icon = "bookmark-check",
					Count = 178,
					Tooltip = "Already yours",
					StartsCountsOnly = true,
				},
			]
		);

		await cut.FindAll("button.change-chip").First().ClickAsync();

		var chips = cut.FindAll(".change-chip");

		await Assert.That(chips[1].TagName).IsNotEqualTo("BUTTON");
		await Assert.That(chips[1].ClassList).DoesNotContain("is-on");
	}

	[Test]
	public async Task Chips_WhenOneIsClicked_FiltersTheListToThatKind()
	{
		var cut = RenderList(
			[Removed("Gone"), Added("Fresh")],
			[
				new ChangeChip
				{
					Icon = "minus",
					Count = 1,
					Tooltip = "Removed",
					Tone = ChipTone.Removed,
					Filters = ChangeKind.Removed,
				},
			]
		);

		await cut.Find(".change-chip").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".change-row")).Count().IsEqualTo(1);
		await Assert.That(cut.Find(".change-row").TextContent).Contains("Gone");
	}

	[Test]
	public async Task Render_WhenARowIsClicked_RaisesTheSelectionWithItsMedia()
	{
		MatchedMedia? selected = null;
		var item = Added("Fresh");
		var cut = RenderList([item], [], media => selected = media);

		await cut.Find(".change-row").ClickAsync(new MouseEventArgs());

		await Assert.That(selected).IsNotNull();
		await Assert.That(selected!.Identity.Id).IsEqualTo(item.Media!.Identity.Id);
	}

	[Test]
	public async Task Dialog_WhenGivenEverySlotItsCallersUse_RendersWithoutAnUnknownParameter()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(ArtworkStub.Sizing());
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = Render<ChangeDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Title, "Review changes before syncing")
				.Add(dialog => dialog.Subtitle, "7 titles compared")
				.Add(dialog => dialog.Items, [Removed("Gone"), Added("Fresh"), Unchanged("Held")])
				.Add(dialog => dialog.Chips, [])
				.Add(dialog => dialog.Fold, Fold(1))
				.Add(dialog => dialog.RemovedLabel, "Removing")
				.Add(dialog => dialog.AddedLabel, "Adding")
				.Add(dialog => dialog.UnchangedLabel, "Already in sync")
				.Add(dialog => dialog.EmptyMessage, "Everything is in sync.")
				.Add(dialog => dialog.Note, "7 runs")
				.Add(dialog => dialog.BandTone, ChangeBandTone.Bad)
				.Add(dialog => dialog.Prologue, builder => builder.AddContent(0, "5 waiting"))
				.Add(dialog => dialog.Band, builder => builder.AddContent(0, "Plex is unreachable"))
				.Add(dialog => dialog.Actions, builder => builder.AddContent(0, "Cancel"))
		);

		await Assert.That(cut.Instance.UnchangedLabel).IsEqualTo("Already in sync");
		await Assert.That(cut.Instance.Items).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Render_ForUnchangedItems_GroupsThemLastUnderTheCallersLabel()
	{
		var cut = RenderList([Unchanged("Held"), Added("Fresh"), Removed("Gone")], [], unchangedLabel: "Already in sync");

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups).Count().IsEqualTo(3);
		await Assert.That(groups[0]).Contains("Removed");
		await Assert.That(groups[2]).Contains("Already in sync");
	}

	[Test]
	public async Task Chips_ForACountThatIsZero_AreOmittedSoZeroIsNeverRenderedAsANumber()
	{
		var cut = RenderList([Added("Fresh")], [Chip("list", 2), Chip("minus", 0, ChangeKind.Removed)]);

		var counts = cut.FindAll(".change-chip-count").Select(chip => chip.TextContent.Trim()).ToList();

		await Assert.That(counts).IsEquivalentTo(["2"]);
		await Assert.That(cut.FindAll(".change-chip")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Chips_WhenTheCountsOnlySectionStartsOnAnOmittedChip_StillDrawTheSeparatorBeforeTheNextOne()
	{
		var kept = Chip("bookmark-check", 0) with { StartsCountsOnly = true };
		var cut = RenderList([Added("Fresh")], [Chip("list", 2), kept, Chip("filter-x", 7)]);

		await Assert.That(cut.FindAll(".change-chip-sep")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll(".change-chip")).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Render_ForARunThatOnlyAdded_ShowsNoRemovedGroup()
	{
		var cut = RenderList(Added("Fresh"), Added("Also Fresh"));

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups).Count().IsEqualTo(1);
		await Assert.That(groups[0]).Contains("Added");
	}

	[Test]
	public async Task Render_ForARunThatOnlyRemoved_ShowsNoAddedGroup()
	{
		var cut = RenderList(Removed("Gone"), Removed("Also Gone"));

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups).Count().IsEqualTo(1);
		await Assert.That(groups[0]).Contains("Removed");
	}

	[Test]
	public async Task Render_ForARunThatChangedNothing_ShowsNoGroupsAtAll()
	{
		var cut = RenderList([], [Chip("list", 180)]);

		await Assert.That(cut.FindAll(".change-group")).IsEmpty();
		await Assert.That(cut.FindAll(".change-row")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenTheCallerPointsAtOneItem_MarksOnlyThatRowAsHighlighted()
	{
		var wanted = MediaId.New();
		var target = Added("Wanted") with { Id = wanted };
		var other = Added("Ignored") with { Id = MediaId.New() };

		var cut = RenderList([target, other], [], highlight: wanted);

		var highlighted = cut.FindAll(".change-row").Where(row => row.ClassList.Contains("is-highlight")).ToList();

		await Assert.That(highlighted).Count().IsEqualTo(1);
		await Assert.That(highlighted[0].TextContent).Contains("Wanted");
	}

	[Test]
	public async Task Highlight_OnceARowIsOpened_StopsMarkingTheRowItPointedAt()
	{
		var wanted = MediaId.New();
		var cut = RenderList([Added("Wanted") with { Id = wanted }], [], highlight: wanted);

		await cut.Find(".change-row").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".change-row.is-highlight")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenTheCallerPointsAtNothing_LeavesEveryRowUnhighlighted()
	{
		var cut = RenderList([Added("Wanted") with { Id = MediaId.New() }], []);

		await Assert.That(cut.FindAll(".change-row.is-highlight")).IsEmpty();
	}

	[Test]
	public async Task Highlight_ForARowFurtherDown_ScrollsTheListToItsSectionAndIndex()
	{
		var module = JSInterop.SetupModule("./js/change-list.js");
		var wanted = MediaId.New();

		RenderList([Removed("Gone"), Added("First"), Added("Second"), Added("Wanted") with { Id = wanted }], [], highlight: wanted);

		var scroll = module.VerifyInvoke("scrollToSection");
		await Assert.That(scroll.Arguments[1]).IsEqualTo("Added");
		await Assert.That(scroll.Arguments[2]).IsEqualTo(2 * 74f);
	}

	[Test]
	public async Task Highlight_WhenItsRowIsRenderedAgain_FocusesItOnlyOnce()
	{
		var wanted = MediaId.New();
		var cut = RenderList([Added("Wanted") with { Id = wanted }, Added("Other")], [], highlight: wanted);

		await cut.Find(".change-search input").InputAsync(new ChangeEventArgs { Value = "Other" });
		await cut.Find(".change-search input").InputAsync(new ChangeEventArgs { Value = string.Empty });

		await Assert.That(cut.FindAll(".change-row.is-highlight")).Count().IsEqualTo(1);
		await Assert.That(JSInterop.Invocations.Count(invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus")).IsEqualTo(1);
	}

	[Test]
	public async Task Render_WhenAKindIsFolded_ShowsOneLineAtTheFootInsteadOfItsGroup()
	{
		var cut = RenderList([Added("Fresh"), Unchanged("Held")], [], fold: Fold(2614), unchangedLabel: "Already in sync");

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups).Count().IsEqualTo(1);
		await Assert.That(groups[0]).Contains("Added");
		await Assert.That(cut.Find(".change-body").LastElementChild!.ClassList).Contains("change-pin");
		await Assert.That(cut.Find(".change-pin").TextContent).Contains($"{2614:N0} titles already in sync, nothing to do");
	}

	[Test]
	public async Task Fold_WhenShowThemIsClicked_AsksForItsRowsAndListsThemLast()
	{
		var module = JSInterop.SetupModule("./js/change-list.js");
		var asked = 0;
		var cut = RenderList([Unchanged("Held"), Added("Fresh"), Unmatched("Nowhere")], [], fold: Fold(1, () => asked++), unchangedLabel: "Already in sync");

		await cut.Find(".change-pin .change-fold").ClickAsync(new MouseEventArgs());

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();
		await Assert.That(asked).IsEqualTo(1);
		await Assert.That(groups).Count().IsEqualTo(3);
		await Assert.That(groups[1]).Contains("Could not be matched");
		await Assert.That(groups[2]).Contains("Already in sync");
		await Assert.That(cut.FindAll(".change-pin")).IsEmpty();
		await Assert.That(module.VerifyInvoke("scrollToSection").Arguments[1]).IsEqualTo("Unchanged");
	}

	[Test]
	public async Task Fold_WhenHideIsClicked_FoldsBackWithoutAskingAgain()
	{
		var asked = 0;
		var cut = RenderList([Unchanged("Held"), Added("Fresh")], [], fold: Fold(1, () => asked++), unchangedLabel: "Already in sync");
		await cut.Find(".change-pin .change-fold").ClickAsync(new MouseEventArgs());

		await cut.Find(".change-group .change-fold").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".change-group")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll(".change-pin")).Count().IsEqualTo(1);
		await cut.Find(".change-pin .change-fold").ClickAsync(new MouseEventArgs());
		await Assert.That(cut.FindAll(".change-group")).Count().IsEqualTo(2);
		await Assert.That(asked).IsEqualTo(1);
	}

	[Test]
	public async Task Fold_WhileASearchIsTyped_SaysItsTitlesWereNotSearched()
	{
		var cut = RenderList([Added("Fresh")], [], fold: Fold(2614));

		await cut.Find(".change-search input").InputAsync(new ChangeEventArgs { Value = "christmas" });

		await Assert.That(cut.Find(".change-pin").TextContent).Contains($"{2614:N0} already in sync were not searched");
	}

	[Test]
	public async Task Chips_WhenAFoldedKindsChipIsPicked_UnfoldsIt()
	{
		var asked = 0;
		var cut = RenderList(
			[Unchanged("Held"), Added("Fresh")],
			[Chip("bookmark-check", 1, ChangeKind.Unchanged)],
			fold: Fold(1, () => asked++),
			unchangedLabel: "Already in sync"
		);

		await cut.Find("button.change-chip").ClickAsync(new MouseEventArgs());

		await Assert.That(asked).IsEqualTo(1);
		await Assert.That(cut.FindAll(".change-row").Select(row => row.TextContent.Trim())).IsEquivalentTo(["Held"]);
		await Assert.That(cut.FindAll(".change-pin")).IsEmpty();
	}

	[Test]
	public async Task Render_ForAPoster_AsksForASizedLazyImage()
	{
		var cut = RenderList(Added("Fresh"));

		var poster = cut.Find("img.change-poster");

		await Assert.That(poster.GetAttribute("loading")).IsEqualTo("lazy");
		await Assert.That(poster.GetAttribute("decoding")).IsEqualTo("async");
		await Assert
			.That(poster.GetAttribute("srcset"))
			.IsEqualTo("https://example.invalid/poster.jpg?width=40 1x, https://example.invalid/poster.jpg?width=80 2x");
	}

	[Test]
	public async Task Render_ForCountsOverAThousand_WritesThemInTheServersNumberFormat()
	{
		var cut = RenderList(
			[.. Enumerable.Range(0, 1200).Select(index => new ChangeItem { Title = $"Fresh {index}", Kind = ChangeKind.Added })],
			[Chip("plus", 2614, ChangeKind.Added)],
			fold: Fold(2614)
		);

		await Assert.That(cut.Find(".change-chip-count").TextContent.Trim()).IsEqualTo(2614.ToString("N0"));
		await Assert.That(cut.Find(".change-group-count").TextContent.Trim()).IsEqualTo(1200.ToString("N0"));
		await Assert.That(cut.Find(".change-pin b").TextContent).IsEqualTo(2614.ToString("N0"));
	}

	private static ChangeFold Fold(int count, Action? unfolding = null)
	{
		return new ChangeFold(
			ChangeKind.Unchanged,
			count,
			() =>
			{
				unfolding?.Invoke();
				return Task.CompletedTask;
			}
		);
	}

	private static ChangeChip Chip(string icon, int count, ChangeKind? filters = null) =>
		new()
		{
			Icon = icon,
			Count = count,
			Tooltip = icon,
			Filters = filters,
		};

	private static ChangeItem Unmatched(string title) => new() { Title = title, Kind = ChangeKind.Unmatched };

	private static ChangeItem Unchanged(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Unchanged,
		};

	private static ChangeItem Removed(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Removed,
		};

	private static ChangeItem Added(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Added,
		};

	private IRenderedComponent<ChangeList> RenderList(params ChangeItem[] items) => RenderList(items, []);

	private IRenderedComponent<ChangeList> RenderList(
		IReadOnlyList<ChangeItem> items,
		IReadOnlyList<ChangeChip> chips,
		Action<MatchedMedia>? onSelect = null,
		string? unchangedLabel = null,
		MediaId? highlight = null,
		ChangeFold? fold = null
	)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(ArtworkStub.Sizing());
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<ChangeList>(parameters =>
		{
			parameters.Add(list => list.Items, items);
			parameters.Add(list => list.Chips, chips);
			if (highlight is not null)
			{
				parameters.Add(list => list.Highlight, highlight);
			}
			if (fold is not null)
			{
				parameters.Add(list => list.Fold, fold);
			}
			if (onSelect is not null)
			{
				parameters.Add(list => list.OnSelect, onSelect);
			}
			if (unchangedLabel is not null)
			{
				parameters.Add(list => list.UnchangedLabel, unchangedLabel);
			}
		});
	}

	private static MatchedMedia Media(string title)
	{
		return new(
			new MediaIdentity(MediaId.New(), [TmdbId.From(title, MediaType.Movie)]),
			new MovieDetails
			{
				Title = title,
				PosterUrl = "https://example.invalid/poster.jpg",
				ReleaseDate = new DateOnly(2024, 1, 1),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(90),
				Ratings = MediaRatings.None,
			}
		);
	}
}
