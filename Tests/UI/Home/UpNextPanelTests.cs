using BlazorBlueprint.Components;
using BlazorBlueprint.Icons.Lucide.Components;
using Bunit;
using Contracts;
using Domain.Jobs;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Home;

public class UpNextPanelTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Note_WithMoreFeedsScheduledThanItLists_CountsThemAll()
	{
		IReadOnlyList<UpNextRow> scheduled =
		[
			.. Enumerable.Range(1, 7).Select(day => new UpNextRow($"feed {day}", "At 04:00", $"in {day} days", UpNextKind.Scheduled, null)),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Scheduled, scheduled));

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("7 feeds on a schedule");
		await Assert.That(cut.FindAll(".row")).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Row_ForAFeed_LinksItsNameToTheFeedEditor()
	{
		IReadOnlyList<UpNextRow> queued =
		[
			new("hdlists/documentaries", "running now", "running", UpNextKind.Running, "/feeds/edit/docs"),
			new("Sync Wantlist", "queued", "2nd in line", UpNextKind.Queued, null),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		var names = cut.FindAll(".row-name");
		await Assert.That(names[0].Closest("a")?.GetAttribute("href")).IsEqualTo("/feeds/edit/docs");
		await Assert.That(names[1].Closest("a")).IsNull();
	}

	[Test]
	public async Task Row_WithABrand_NamesItsProviderInTheLogo()
	{
		IReadOnlyList<UpNextRow> queued =
		[
			new("hdlists/documentaries", "queued", string.Empty, UpNextKind.Queued, "/feeds/edit/docs", MediaProvider.Mdblist),
			new("Sync Wantlist", "queued", string.Empty, UpNextKind.Queued, null, MediaProvider.Plex),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		await Assert
			.That(cut.FindAll(".row .provider-tile img").Select(logo => logo.GetAttribute("alt") ?? string.Empty))
			.IsEquivalentTo(["Movie Database List", "Plex"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Row_WhileRunning_CarriesTheSweepAndTheOthersDoNot()
	{
		IReadOnlyList<UpNextRow> queued =
		[
			new("hdlists/documentaries", "running now", "running", UpNextKind.Running, "/feeds/edit/docs"),
			new("snoak/latest-movies-digital-release", "queued", string.Empty, UpNextKind.Queued, "/feeds/edit/latest"),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		var rows = cut.FindAll(".row").Select(row => row.ClassList.Contains("is-running"));
		await Assert.That(rows).IsEquivalentTo([true, false]);
	}

	[Test]
	public async Task Row_ForAFeedThatWasDeleted_ShowsAGhostInPlaceOfTheStateGlyph()
	{
		IReadOnlyList<UpNextRow> queued = [new("A deleted feed", "queued", string.Empty, UpNextKind.Queued, null, Deleted: true)];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		var ghost = Render<LucideIcon>(parameters => parameters.Add(icon => icon.Name, "ghost").Add(icon => icon.Size, 12));
		await Assert.That(cut.Find(".row-icon path").GetAttribute("d")).IsEqualTo(ghost.Find("path").GetAttribute("d"));
	}

	[Test]
	public async Task Row_WhileWaiting_CountsDownToWhenItRuns()
	{
		var now = new DateTimeOffset(2026, 9, 30, 14, 0, 0, TimeSpan.Zero);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(now);
		Services.AddSingleton(clock.Object);
		IReadOnlyList<UpNextRow> queued =
		[
			new("Sync Wantlist", "waiting for grace period", string.Empty, UpNextKind.Waiting, null) { RunsAt = Some.Of(now.AddMinutes(2)) },
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		await Assert.That(cut.Find(".row-when").TextContent.Trim()).IsEqualTo("in 2 min");
	}

	[Test]
	public async Task Cancel_OnAJobThatHasNotStarted_HandsItsRowToTheCaller()
	{
		var job = JobId.From(Guid.NewGuid());
		IReadOnlyList<UpNextRow> queued =
		[
			new("hdlists/documentaries", "running now", "running", UpNextKind.Running, null) { Job = Some.Of(JobId.From(Guid.NewGuid())) },
			new("hdlists/anime", "queued", string.Empty, UpNextKind.Queued, null) { Job = Some.Of(job) },
		];
		UpNextRow? cancelled = null;

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued).Add(panel => panel.OnCancel, (UpNextRow row) => cancelled = row));
		await cut.Find("[aria-label='Cancel hdlists/anime']").ClickAsync(new());

		await Assert.That(cut.FindAll(".row-cancel")).Count().IsEqualTo(1);
		await Assert.That(cancelled!.Job).IsSome().And.IsEqualTo(job);
	}

	[Test]
	public async Task Sections_WhenEmpty_ShowCollapsedAndOpenOntoAPlaceholder()
	{
		var cut = Render<UpNextPanel>();
		var shownAtFirst = cut.FindAll(".sect-empty").Count;

		await cut.FindAll(".sect")[0].ClickAsync(new());

		await Assert.That(cut.FindAll(".sect-title").Select(title => title.TextContent)).IsEquivalentTo(["Running and queued", "On a schedule"]);
		await Assert.That(shownAtFirst).IsEqualTo(0);
		await Assert.That(cut.FindAll(".sect-empty").Select(placeholder => placeholder.TextContent)).IsEquivalentTo(["Nothing is running or queued right now."]);
	}

	[Test]
	public async Task Queue_WhenAJobArrivesWhileItIsCollapsedForBeingEmpty_OpensToShowIt()
	{
		var cut = Render<UpNextPanel>();

		cut.Render(parameters => parameters.Add(panel => panel.Queued, [new UpNextRow("hdlists/anime", "queued", string.Empty, UpNextKind.Queued, null)]));

		await Assert.That(cut.FindAll(".row")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Note_WithNothingQueuedOrScheduled_SaysNothingIsScheduled()
	{
		var cut = Render<UpNextPanel>();

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("nothing scheduled");
	}
}
