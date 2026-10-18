using Domain.Media;

namespace Contracts.Processing;

public sealed record MediaQueryRow(MatchedMedia Media, Optional<SammlarrRating> Sammlarr)
{
	public static MediaQueryRow From(MatchedMedia media, RatingStrategy strategy, ValueSet<RatingSource> available)
	{
		return new(media, strategy.Resolve(media.Details.Ratings, available));
	}

	public MediaType MediaType => Media.Details.Type;

	public string? Title => Media.Details.Title;

	public string? Tagline => Media.Details.Tagline;

	public string? Summary => Media.Details.Summary;

	public IReadOnlyList<string> Genres => [.. Media.Details.Genres];

	public DateOnly? ReleaseDate => Media.Details.ReleaseDate;

	public double? Rating => Sammlarr.Map(rating => rating.Rounded).ToNullable();

	public int? Votes => Sammlarr.Map(rating => rating.Votes).ToNullable();

	public int? RuntimeMinutes
	{
		get
		{
			return Media.Details switch
			{
				ShowDetails show => Minutes(show.RuntimePerEpisode),
				MovieDetails movie => Minutes(movie.Runtime),
				UnknownDetails => null,
			};
		}
	}

	public int? TotalRuntimeMinutes
	{
		get
		{
			return Media.Details switch
			{
				ShowDetails show => Minutes(show.RuntimePerEpisode * show.Episodes),
				MovieDetails movie => Minutes(movie.Runtime),
				UnknownDetails => null,
			};
		}
	}

	public int? Seasons => Media.Details is ShowDetails show ? show.Seasons : null;

	public int? Episodes => Media.Details is ShowDetails show ? show.Episodes : null;

	private static int? Minutes(TimeSpan? runtime) => runtime is { } value ? (int)Math.Round(value.TotalMinutes) : null;
}
