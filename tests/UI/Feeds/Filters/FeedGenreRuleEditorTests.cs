using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Media;
using Domain.Processing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor.Filters;

namespace UI.Tests.Feeds.Filters;

public class FeedGenreRuleEditorTests : BunitContext
{
	private static readonly IReadOnlyList<GenreCount> Genres =
	[
		new("Animation", 377),
		new("Anime", 322),
		new("Drama", 285),
		new("Sci-Fi & Fantasy", 200),
		new("Science Fiction", 99),
	];

	private readonly List<GenreMatch> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		var media = IMediaService.Mock();
		media.GetGenresAsync(Any()).Returns(Genres);
		Services.AddSingleton(media.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Open_WithNothingPicked_ListsEveryGenreMostUsedFirstWithItsCount()
	{
		var cut = await OpenAsync(GenreMatch.None);

		await Assert
			.That(Options(cut).Select(Row))
			.IsEquivalentTo(["Animation 377", "Anime 322", "Drama 285", "Sci-Fi & Fantasy 200", "Science Fiction 99"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Open_WithAPickNoTitleCarriesAnyMore_ListsItFirstSoItCanBeUnpicked()
	{
		var cut = await OpenAsync(new GenreMatch(["Film Noir"], []));

		await Assert.That(Row(Options(cut).First())).IsEqualTo("Film Noir 0");
	}

	[Test]
	public async Task GenreRow_ForAPickedGenre_MarksTheRowAndHidesTheBoxFromReaders()
	{
		var cut = await OpenAsync(new GenreMatch(["Drama"], []));

		var drama = Option(cut, "Drama");

		await Assert.That(drama.GetAttribute("data-picked")).IsEqualTo("true");
		await Assert.That(drama.FirstElementChild!.GetAttribute("aria-hidden")).IsEqualTo("true");
	}

	[Test]
	public async Task Genre_WhenClicked_IsPickedBesideTheOthers()
	{
		var cut = await OpenAsync(new GenreMatch(["Drama"], []));

		await Option(cut, "Anime").ClickAsync(new());

		await Assert.That(_changes.Last().Genres).IsEquivalentTo(["Drama", "Anime"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Genre_WhenClickedAgain_IsUnpicked()
	{
		var cut = await OpenAsync(new GenreMatch(["Drama", "Anime"], []));

		await Option(cut, "Drama").ClickAsync(new());

		await Assert.That(_changes.Last().Genres).IsEquivalentTo(["Anime"]);
	}

	[Test]
	public async Task Genre_WhenClickedWhileSearching_IsPicked()
	{
		var cut = await OpenAsync(GenreMatch.None);
		await SearchAsync(cut, "sci");

		await Option(cut, "Science Fiction").ClickAsync(new());

		await Assert.That(_changes.Last().Genres).IsEquivalentTo(["Science Fiction"]);
	}

	[Test]
	public async Task Search_ForTextNoGenreIsNamed_OffersItAsTextWithTheGenresItCatches()
	{
		var cut = await OpenAsync(GenreMatch.None);

		await SearchAsync(cut, "sci");

		await Assert.That(cut.Find("[data-genre-add]").TextContent).Contains("\"sci\"").And.Contains("matches 2 genres");
	}

	[Test]
	[Arguments("anime")]
	[Arguments("anim")]
	public async Task Search_ForAGenresNameOrATextAlreadyAdded_OffersNoText(string typed)
	{
		var cut = await OpenAsync(new GenreMatch([], ["anim"]));

		await SearchAsync(cut, typed);

		await Assert.That(cut.FindAll("[data-genre-add]")).IsEmpty();
	}

	[Test]
	public async Task AddText_WhenClicked_KeepsThePickedGenresAndAddsTheText()
	{
		var cut = await OpenAsync(new GenreMatch(["Drama"], []));
		await SearchAsync(cut, "anim");

		await cut.Find("[data-genre-add]").ClickAsync(new());

		await Assert.That(_changes.Last().Genres).IsEquivalentTo(["Drama"]);
		await Assert.That(_changes.Last().Texts).IsEquivalentTo(["anim"]);
	}

	[Test]
	public async Task TypedText_WhenRemoved_LeavesThePicks()
	{
		var cut = await OpenAsync(new GenreMatch(["Drama"], ["anim"]));

		await cut.Find("button[aria-label='Remove anim']").ClickAsync(new());

		await Assert.That(_changes.Last().Genres).IsEquivalentTo(["Drama"]);
		await Assert.That(_changes.Last().Texts).IsEmpty();
	}

	[Test]
	public async Task Trigger_ForPicksAndText_ShowsTheTextApart()
	{
		var cut = RenderEditor(new GenreMatch(["Drama"], ["anim"]));

		await Assert.That(cut.Find("[data-genre-chip]").TextContent.Trim()).IsEqualTo("Drama");
		await Assert.That(cut.Find("[data-genre-text]").TextContent.Trim()).IsEqualTo("anim");
	}

	private static IEnumerable<IElement> Options(IRenderedComponent<IComponent> cut) => cut.FindAll("[data-genre-option]");

	private static IElement Option(IRenderedComponent<IComponent> cut, string genre) => Options(cut).Single(option => Name(option) == genre);

	private static string Name(IElement option) => option.QuerySelector("[data-genre-name]")!.TextContent.Trim();

	private static string Row(IElement option) => $"{Name(option)} {option.QuerySelector("[data-genre-count]")!.TextContent.Trim()}";

	private static async Task SearchAsync(IRenderedComponent<IComponent> cut, string text)
	{
		await cut.InvokeAsync(() => cut.FindComponent<BbCommand>().Instance.SearchQueryChanged.InvokeAsync(text));
	}

	private async Task<IRenderedComponent<IComponent>> OpenAsync(GenreMatch value)
	{
		var cut = RenderEditor(value);
		await cut.Find("button[aria-label='Genres']").ClickAsync(new());
		return cut;
	}

	private IRenderedComponent<IComponent> RenderEditor(GenreMatch value)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedGenreRuleEditor>(1);
			builder.AddComponentParameter(2, nameof(FeedGenreRuleEditor.Value), value);
			builder.AddComponentParameter(3, nameof(FeedGenreRuleEditor.ValueChanged), EventCallback.Factory.Create<GenreMatch>(this, _changes.Add));
			builder.AddComponentParameter(4, nameof(FeedGenreRuleEditor.Label), "Genres");
			builder.CloseComponent();
		});
	}
}
