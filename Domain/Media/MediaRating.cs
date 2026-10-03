using System.Globalization;

namespace Domain.Media;

public interface IMediaRating
{
	RatingSource Source { get; }

	string Value { get; }

	float SammlarrValue { get; }

	Optional<int> Votes { get; }

	RatingMark Mark { get; }
}

public readonly record struct TmdbRating(float Average, Optional<int> Votes) : IMediaRating
{
	public RatingSource Source => RatingSource.Tmdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public RatingMark Mark => RatingMark.Tmdb;
}

public readonly record struct ImdbRating(float Average, Optional<int> Votes) : IMediaRating
{
	public RatingSource Source => RatingSource.Imdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public RatingMark Mark => RatingMark.Imdb;
}

public readonly record struct RottenTomatoesCriticsRating(int Percent) : IMediaRating
{
	private const float Ceiling = 100;

	private const int Fresh = 60;

	internal static RottenTomatoesCriticsRating Of(float sammlarrValue) => new((int)MathF.Round(sammlarrValue / 10 * Ceiling));

	public RatingSource Source => RatingSource.RottenTomatoesCritics;

	public string Value => $"{Percent}%";

	public float SammlarrValue => Percent * 10 / Ceiling;

	public Optional<int> Votes => new None();

	public RatingMark Mark => Percent >= Fresh ? RatingMark.Fresh : RatingMark.Rotten;
}

public readonly record struct RottenTomatoesAudienceRating(int Percent) : IMediaRating
{
	private const float Ceiling = 100;

	private const int Upright = 60;

	internal static RottenTomatoesAudienceRating Of(float sammlarrValue) => new((int)MathF.Round(sammlarrValue / 10 * Ceiling));

	public RatingSource Source => RatingSource.RottenTomatoesAudience;

	public string Value => $"{Percent}%";

	public float SammlarrValue => Percent * 10 / Ceiling;

	public Optional<int> Votes => new None();

	public RatingMark Mark => Percent >= Upright ? RatingMark.Upright : RatingMark.Spilled;
}

public readonly record struct RatingSummary(ValueSet<IMediaRating> Sources)
{
	public static readonly RatingSummary None = new([]);

	public static RatingSummary From(IEnumerable<IMediaRating> sources) => new([.. sources.DistinctBy(rating => rating.Source)]);

	public Optional<IMediaRating> Of(RatingSource source) => Sources.FirstOrNone(rating => rating.Source == source);

	public RatingSummary FillFrom(RatingSummary other)
	{
		return From([.. Sources.Select(mine => other.Of(mine.Source).When(theirs => Richer(theirs, mine)).GetValueOr(mine)), .. other.Sources]);
	}

	private static bool Richer(IMediaRating candidate, IMediaRating current) => candidate.Votes.IsSome() && current.Votes.IsNone();
}
