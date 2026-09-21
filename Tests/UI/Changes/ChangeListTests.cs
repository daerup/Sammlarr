using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Changes;

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
		var entry = Removed("Gone") with { ContextIcon = "log-out", ContextTooltip = "Absent from 3 of 3 runs" };

		var cut = RenderList(entry);

		await Assert.That(cut.FindAll(".change-context")).HasCount(1);
		await Assert.That(cut.Find(".change-row").TextContent).DoesNotContain("Absent from");
	}

	[Test]
	public async Task Render_ForAnUnmatchedRow_UsesAPlaceholderInsteadOfAPoster()
	{
		var cut = RenderList(new ChangeEntry { Title = "Nowhere", Kind = ChangeKind.Unmatched });

		await Assert.That(cut.FindAll(".change-poster.is-missing")).HasCount(1);
		await Assert.That(cut.FindAll("img.change-poster")).IsEmpty();
	}

	[Test]
	public async Task Chips_WhenOneHasNoItemsBehindIt_AreDimmedAndInert()
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

		await Assert.That(chips[0].ClassList).DoesNotContain("is-dim");
		await Assert.That(chips[1].ClassList).Contains("is-dim");
		await Assert.That(chips[1].HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.FindAll(".change-chip-sep")).HasCount(1);
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

		await Assert.That(cut.FindAll(".change-row")).HasCount(1);
		await Assert.That(cut.Find(".change-row").TextContent).Contains("Gone");
	}

	[Test]
	public async Task Render_WhenARowIsClicked_RaisesTheSelectionWithItsMedia()
	{
		MediaLookup? selected = null;
		var entry = Added("Fresh");
		var cut = RenderList([entry], [], media => selected = media);

		await cut.Find(".change-row").ClickAsync(new MouseEventArgs());

		await Assert.That(selected).IsNotNull();
		await Assert.That(selected!.Value.AsIdentified()!.Value.Identity.Id).IsEqualTo(entry.Media!.Value.AsIdentified()!.Value.Identity.Id);
	}

	[Test]
	public async Task Dialog_WhenGivenEverySlotItsCallersUse_RendersWithoutAnUnknownParameter()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = Render<ChangeDialog>(parameters =>
			parameters
				.Add(dialog => dialog.Open, true)
				.Add(dialog => dialog.Title, "Review changes before syncing")
				.Add(dialog => dialog.Subtitle, "7 titles compared")
				.Add(dialog => dialog.Entries, [Removed("Gone"), Added("Fresh"), Unchanged("Held")])
				.Add(dialog => dialog.Chips, [])
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
		await Assert.That(cut.Instance.Entries).HasCount(3);
	}

	[Test]
	public async Task Render_ForUnchangedEntries_GroupsThemLastUnderTheCallersLabel()
	{
		var cut = RenderList([Unchanged("Held"), Added("Fresh"), Removed("Gone")], [], unchangedLabel: "Already in sync");

		var groups = cut.FindAll(".change-group").Select(group => group.TextContent.Trim()).ToList();

		await Assert.That(groups).HasCount(3);
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
		var wanted = MediaId.From(Guid.NewGuid());
		var target = Added("Wanted") with { Id = wanted };
		var other = Added("Ignored") with { Id = MediaId.From(Guid.NewGuid()) };

		var cut = RenderList([target, other], [], highlight: wanted);

		var highlighted = cut.FindAll(".change-row").Where(row => row.ClassList.Contains("is-highlight")).ToList();

		await Assert.That(highlighted).HasCount(1);
		await Assert.That(highlighted[0].TextContent).Contains("Wanted");
	}

	[Test]
	public async Task Highlight_OnceARowIsOpened_StopsMarkingTheRowItPointedAt()
	{
		var wanted = MediaId.From(Guid.NewGuid());
		var cut = RenderList([Added("Wanted") with { Id = wanted }], [], highlight: wanted);

		await cut.Find(".change-row").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".change-row.is-highlight")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenTheCallerPointsAtNothing_LeavesEveryRowUnhighlighted()
	{
		var cut = RenderList([Added("Wanted") with { Id = MediaId.From(Guid.NewGuid()) }], []);

		await Assert.That(cut.FindAll(".change-row.is-highlight")).IsEmpty();
	}

	private static ChangeChip Chip(string icon, int count, ChangeKind? filters = null) =>
		new()
		{
			Icon = icon,
			Count = count,
			Tooltip = icon,
			Filters = filters,
		};

	private static ChangeEntry Unchanged(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Unchanged,
		};

	private static ChangeEntry Removed(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Removed,
		};

	private static ChangeEntry Added(string title) =>
		new()
		{
			Title = title,
			Media = Media(title),
			Kind = ChangeKind.Added,
		};

	private IRenderedComponent<ChangeList> RenderList(params ChangeEntry[] entries) => RenderList(entries, []);

	private IRenderedComponent<ChangeList> RenderList(
		IReadOnlyList<ChangeEntry> entries,
		IReadOnlyList<ChangeChip> chips,
		Action<MediaLookup>? onSelect = null,
		string? unchangedLabel = null,
		MediaId? highlight = null
	)
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<ChangeList>(parameters =>
		{
			parameters.Add(list => list.Entries, entries);
			parameters.Add(list => list.Chips, chips);
			if (highlight is not null)
			{
				parameters.Add(list => list.Highlight, highlight);
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

	private static MediaLookup Media(string title) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From(title, MediaType.Movie)]),
			Title = title,
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = "https://example.invalid/poster.jpg",
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Rating = RatingSummary.None,
		}.ToMatch();
}
