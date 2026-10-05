using System.Globalization;

namespace Domain.Media;

public interface IMediaRating
{
	RatingSource Source { get; }

	string Value { get; }

	float SammlarrValue { get; }

	Optional<int> Votes { get; }

	Optional<RatingVerdict> Verdict { get; }
}

public readonly record struct TmdbRating(float Average, Optional<int> Votes) : IMediaRating
{
	public RatingSource Source => RatingSource.Tmdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct ImdbRating(float Average, Optional<int> Votes) : IMediaRating
{
	public RatingSource Source => RatingSource.Imdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public Optional<RatingVerdict> Verdict => new None();
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

	public Optional<RatingVerdict> Verdict => Some.Of(Percent >= Fresh ? RatingVerdict.Liked : RatingVerdict.Disliked);
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

	public Optional<RatingVerdict> Verdict => Some.Of(Percent >= Upright ? RatingVerdict.Liked : RatingVerdict.Disliked);
}

public readonly record struct MediaRatings(ValueSet<IMediaRating> All)
{
	public static readonly MediaRatings None = new([]);

	public static MediaRatings From(IEnumerable<IMediaRating> ratings) => new([.. ratings.DistinctBy(rating => rating.Source)]);

	public Optional<IMediaRating> Of(RatingSource source) => All.FirstOrNone(rating => rating.Source == source);

	public MediaRatings FillFrom(MediaRatings other)
	{
		return From([.. All.Select(mine => other.Of(mine.Source).When(theirs => Richer(theirs, mine)).GetValueOr(mine)), .. other.All]);
	}

	private static bool Richer(IMediaRating candidate, IMediaRating current) => candidate.Votes.IsSome() && current.Votes.IsNone();
}
