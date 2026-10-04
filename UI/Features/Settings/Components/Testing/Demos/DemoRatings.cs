using Domain.Media;

namespace UI.Features.Settings.Components.Testing.Demos;

internal static class DemoRatings
{
	internal static readonly ValueSet<RatingSource> Everything = [.. Enum.GetValues<RatingSource>()];

	internal static readonly MediaRatings Digger = MediaRatings.From([
		new TmdbRating(7.4f, Some.Of(109)),
		new ImdbRating(7.3f, Some.Of(6382)),
		new RottenTomatoesCriticsRating(51),
		new RottenTomatoesAudienceRating(71),
	]);

	internal static readonly MediaRatings Shining = MediaRatings.From([
		new TmdbRating(9.6f, Some.Of(2210)),
		new ImdbRating(9.5f, Some.Of(48210)),
		new RottenTomatoesCriticsRating(98),
	]);

	internal static readonly MediaRatings WithoutImdb = MediaRatings.From([new TmdbRating(6.8f, Some.Of(64)), new RottenTomatoesAudienceRating(58)]);

	internal static readonly RatingStrategy Average = new(AveragedRating.Everything);

	internal static readonly RatingStrategy FirstAvailable = new(new OrderedRating([RatingSource.Imdb, RatingSource.Tmdb]));

	internal static readonly RatingStrategy RottenTomatoesAlone = new(new AveragedRating([RatingSource.RottenTomatoesCritics]));
}
