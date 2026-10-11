using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Identity;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Housekeeping;
using UI.Shared.Services;
using UI.Tests.Media;
using static UI.Tests.Settings.Housekeeping.StoredTwicePairs;

namespace UI.Tests.Settings.Housekeeping;

public class TitlesStoredTwiceDialogTests : BunitContext
{
	private static readonly StoredTwice Dune = Pair(
		Title("Dune: Part Two", 2024, TmdbId.From("693134", MediaType.Movie)),
		Title("Dune: Part Two", 2024, ImdbId.From("tt15239678", MediaType.Movie)),
		new Undecided(),
		2
	);

	private static readonly StoredTwice Fellowship = Pair(
		Title("The Fellowship of the Ring", 2001, TmdbId.From("120", MediaType.Movie)),
		Title(null, null, ImdbId.From("tt0120737", MediaType.Movie)),
		new Undecided(),
		0
	);

	private static readonly StoredTwice Aladdin = Pair(
		Title("Aladdin", 1992, TmdbId.From("812", MediaType.Movie), ImdbId.From("tt0103639", MediaType.Movie)),
		Title("Aladdin", 2019, TmdbId.From("420817", MediaType.Movie)),
		new Distinct(new Contradiction(MediaProvider.Tmdb), Now),
		1
	);

	private static readonly StoredTwice Annie = Pair(
		Title("Annie", 1982, ImdbId.From("tt0083564", MediaType.Movie)),
		Title("Annie", 2014, TmdbId.From("196867", MediaType.Movie)),
		new Distinct(new AnsweredElsewhere(RemoteService.Mdblist, TmdbId.From("15739", MediaType.Movie)), Now),
		1
	);

	private readonly Mock<IConflictReview> _review = IConflictReview.Mock();

	private readonly List<bool> _opened = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddSingleton(_review.Object);
		_review.PairsAsync(Any()).Returns([Fellowship, Aladdin, Dune, Annie]);
		_review.ApplyAsync(Any(), Any()).Returns([]);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ForPairsToDecideAndKeptApart_ListsTriedPairsFirstThenNewOnesThenTheKeptApart()
	{
		var cut = RenderOpen();

		var rail = cut.FindAll("[data-testid=stored-twice-item]");

		await Assert
			.That(rail.Select(item => item.QuerySelector("b")!.TextContent))
			.IsEquivalentTo(["Dune: Part Two", "The Fellowship of the Ring", "Aladdin", "Annie"], CollectionOrdering.Matching);
		await Assert
			.That(rail.Select(item => item.QuerySelector(".st-dot")!.ClassList.Contains("is-tried")))
			.IsEquivalentTo([true, false, false, false], CollectionOrdering.Matching);
		await Assert.That(Text(cut, "stored-twice-progress")).IsEqualTo("Pair 1 of 2");
	}

	[Test]
	public async Task Render_ForATitleWhoseDetailsWereTrimmed_ShowsOnlyItsIds()
	{
		var cut = RenderOpen();

		await Rail(cut, "The Fellowship of the Ring").ClickAsync(new());

		var trimmed = cut.FindAll("[data-testid=stored-twice-title]")[1];
		await Assert.That(trimmed.TextContent).Contains("No details stored, only its ids").And.Contains("tt0120737");
	}

	[Test]
	public async Task Render_ForAPairKeptApartBecauseBothHoldATmdbId_MarksTheTmdbIdsRedAndOffersNoMerge()
	{
		var cut = RenderOpen();

		await Rail(cut, "Aladdin").ClickAsync(new());

		var clashing = cut.FindAll("[data-testid=stored-twice-id]").Where(id => id.ClassList.Contains("is-clash")).Select(id => id.TextContent.Trim());
		await Assert.That(clashing).IsEquivalentTo(["812", "420817"]);
		await Assert.That(Text(cut, "stored-twice-actions")).Contains("can't be merged").And.DoesNotContain("Merge");
		await Assert.That(cut.FindAll("[data-testid=stored-twice-year]").Select(year => year.ClassList.Contains("st-year-differs"))).IsEquivalentTo([true, true]);
	}

	[Test]
	public async Task Back_AfterKeepingAPairApart_ReturnsToItWithItsChoice()
	{
		var cut = RenderOpen();
		await Button(cut, "Keep apart").ClickAsync(new());

		await Button(cut, "Back").ClickAsync(new());

		await Assert.That(Text(cut, "stored-twice-progress")).IsEqualTo("Pair 1 of 2");
		await Assert.That(Text(cut, "stored-twice-actions")).Contains("Will keep apart");
	}

	[Test]
	public async Task Apply_AfterDecidingEveryPair_ChangesNothingBeforeItAndThenSendsTheChoices()
	{
		var cut = RenderOpen();
		await Button(cut, "Merge").ClickAsync(new());
		await Button(cut, "Keep apart").ClickAsync(new());

		var summary = Text(cut, "stored-twice-summary");
		_review.ApplyAsync(Any(), Any()).WasNeverCalled();
		await Button(cut, "Apply 2").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll("[data-testid=stored-twice-applied]").Count == 1);

		await Assert.That(summary).Contains("Will merge · Dune: Part Two (2024)").And.Contains("Will keep apart · The Fellowship of the Ring (2001)");
		await Assert.That(summary).Contains("Stays kept apart · Aladdin (1992 and 2019)");
		_review
			.ApplyAsync(
				Is<IReadOnlyList<ReviewChoice>>(choices => choices!.Select(choice => choice.Choice).SequenceEqual([ConflictChoice.Merge, ConflictChoice.KeepApart])),
				Any()
			)
			.WasCalled(Times.Once);
		await Assert.That(Text(cut, "stored-twice-applied")).Contains("Merged 1 · kept apart 1 · left to the nightly pass 0");
		await Assert.That(cut.FindAll("[data-testid=stored-twice-item]").All(item => item.HasAttribute("disabled"))).IsTrue();
	}

	[Test]
	public async Task Close_FromTheHeader_ClosesTheDialog()
	{
		var cut = RenderOpen();

		await cut.Find("[data-testid=stored-twice-close]").ClickAsync(new());

		await Assert.That(_opened).IsEquivalentTo([false]);
	}

	[Test]
	public async Task Apply_AfterMergingAKeptApartPairAnyway_CountsItAsMergedAndTheUndecidedAsLeft()
	{
		var cut = RenderOpen();
		await Rail(cut, "Annie").ClickAsync(new());

		await Button(cut, "Merge anyway").ClickAsync(new());
		await Button(cut, "Apply 1").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll("[data-testid=stored-twice-applied]").Count == 1);

		await Assert.That(Text(cut, "stored-twice-applied")).Contains("Merged 1 · kept apart 0 · left to the nightly pass 2");
	}

	[Test]
	public async Task Apply_WhenEveryPairIsLeftToTheNightlyPass_HasNothingToApply()
	{
		var cut = RenderOpen();

		await Button(cut, "Leave to the nightly pass").ClickAsync(new());
		await Button(cut, "Leave to the nightly pass").ClickAsync(new());

		await Assert.That(Text(cut, "stored-twice-summary")).StartsWith("Nothing to apply");
		await Assert.That(Button(cut, "Apply 0").HasAttribute("disabled")).IsTrue();
	}

	private IRenderedComponent<IComponent> RenderOpen()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Changed, open => _opened.Add(open)));
		cut.WaitForState(() => cut.FindAll("[data-testid=stored-twice-item]").Count == 4);
		return cut;
	}

	private static IElement Rail(IRenderedComponent<IComponent> cut, string name)
	{
		return cut.FindAll("[data-testid=stored-twice-item]").Single(item => item.QuerySelector("b")!.TextContent == name);
	}

	private static IElement Button(IRenderedComponent<IComponent> cut, string label)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
	}

	private static string Text(IRenderedComponent<IComponent> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public EventCallback<bool> Changed { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<TitlesStoredTwiceDialog>(1);
			builder.AddAttribute(2, nameof(TitlesStoredTwiceDialog.Open), true);
			builder.AddAttribute(3, nameof(TitlesStoredTwiceDialog.OpenChanged), Changed);
			builder.CloseComponent();
		}
	}
}
