using Bunit;
using Domain.Media;
using TestInfrastructure;
using UI.Shared.Media.Ratings;

namespace UI.Tests.Media.Ratings;

public class RatingMarkIconTests : BunitContext
{
	private static readonly string ProvidersFolder = RepositoryRoot.Resolve("src", "UI", "wwwroot", "providers");

	[Before(Test)]
	public void Setup() => Services.AddProviderBranding();

	[Test]
	[Arguments(87, "rottentomatoescritics-liked.svg")]
	[Arguments(42, "rottentomatoescritics-disliked.svg")]
	public async Task Mark_ForARottenTomatoesCriticsRating_ShowsItsVerdict(int percent, string file)
	{
		var cut = Render<RatingMarkIcon>(parameters =>
			parameters
				.Add(icon => icon.Source, RatingSource.RottenTomatoesCritics)
				.Add(icon => icon.Rating, Some.Of<IMediaRating>(new RottenTomatoesCriticsRating(percent)))
		);

		await Assert.That(cut.Find("img.rating-mark").GetAttribute("src")).IsEqualTo($"_content/UI/providers/ratings/{file}");
	}

	[Test]
	public async Task Mark_ForARottenTomatoesAudienceRowWithoutARating_ShowsRottenTomatoesTile()
	{
		var cut = Render<RatingMarkIcon>(parameters => parameters.Add(icon => icon.Source, RatingSource.RottenTomatoesAudience));

		await Assert.That(cut.FindAll("img.rating-mark")).IsEmpty();
		await Assert.That(cut.Markup).Contains("rottentomatoes-square.svg");
	}

	[Test]
	public async Task Mark_ForATmdbRating_ShowsTmdbsTile()
	{
		var cut = Render<RatingMarkIcon>(parameters =>
			parameters.Add(icon => icon.Source, RatingSource.Tmdb).Add(icon => icon.Rating, Some.Of<IMediaRating>(new TmdbRating(7.8f, Some.Of(12_000))))
		);

		await Assert.That(cut.FindAll("img.rating-mark")).IsEmpty();
		await Assert.That(cut.Markup).Contains("tmdb-logo.svg");
	}

	[Test]
	public async Task VerdictArt_ForEveryVerdictASourceCanGive_IsAFileThatExists()
	{
		var shown = Enum.GetValues<RatingSource>()
			.SelectMany(source => new[] { 0f, 10f }.Select(value => source.Card.Rate(value, new None())))
			.Where(rating => rating.Verdict.IsSome())
			.Select(rating => Render<RatingMarkIcon>(parameters => parameters.Add(icon => icon.Source, rating.Source).Add(icon => icon.Rating, Some.Of(rating))))
			.Select(cut => cut.Find("img.rating-mark").GetAttribute("src") ?? string.Empty)
			.ToList();

		await Assert.That(shown).Count().IsEqualTo(6);
		await Assert.That(shown.Where(src => !File.Exists(Path.Combine(ProvidersFolder, src.Replace("_content/UI/providers/", string.Empty))))).IsEmpty();
	}
}
