using System.Globalization;
using Domain.Providers;

namespace Domain.Media;

public interface IMediaRating
{
	RatingSource Source { get; }

	string Value { get; }

	float SammlarrValue { get; }

	Optional<int> Votes { get; }

	Optional<RatingVerdict> Verdict { get; }
}

public interface IOwnedRating<TSelf> : IMediaRating
	where TSelf : struct, IOwnedRating<TSelf>
{
	static abstract MediaProvider Owner { get; }

	static abstract bool PublishesVotes { get; }

	static abstract TSelf Rate(float sammlarrValue, Optional<int> votes);
}

public readonly record struct TmdbRating(float Average, Optional<int> Votes) : IOwnedRating<TmdbRating>
{
	public static MediaProvider Owner => MediaProvider.Tmdb;

	public static bool PublishesVotes => true;

	public static TmdbRating Rate(float sammlarrValue, Optional<int> votes) => new(sammlarrValue, votes);

	public RatingSource Source => RatingSource.Tmdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct ImdbRating(float Average, Optional<int> Votes) : IOwnedRating<ImdbRating>
{
	public static MediaProvider Owner => MediaProvider.Imdb;

	public static bool PublishesVotes => true;

	public static ImdbRating Rate(float sammlarrValue, Optional<int> votes) => new(sammlarrValue, votes);

	public RatingSource Source => RatingSource.Imdb;

	public string Value => Average.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Average;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct RottenTomatoesCriticsRating(int Percent) : IOwnedRating<RottenTomatoesCriticsRating>
{
	private const float Ceiling = 100;

	private const int Fresh = 60;

	public static MediaProvider Owner => MediaProvider.RottenTomatoes;

	public static bool PublishesVotes => false;

	public static RottenTomatoesCriticsRating Rate(float sammlarrValue, Optional<int> votes) => new((int)MathF.Round(sammlarrValue / 10 * Ceiling));

	public RatingSource Source => RatingSource.RottenTomatoesCritics;

	public string Value => FormattableString.Invariant($"{Percent}%");

	public float SammlarrValue => Percent * 10 / Ceiling;

	public Optional<int> Votes => new None();

	public Optional<RatingVerdict> Verdict => Some.Of(Percent >= Fresh ? RatingVerdict.Liked : RatingVerdict.Disliked);
}

public readonly record struct RottenTomatoesAudienceRating(int Percent) : IOwnedRating<RottenTomatoesAudienceRating>
{
	private const float Ceiling = 100;

	private const int Upright = 60;

	public static MediaProvider Owner => MediaProvider.RottenTomatoes;

	public static bool PublishesVotes => false;

	public static RottenTomatoesAudienceRating Rate(float sammlarrValue, Optional<int> votes) => new((int)MathF.Round(sammlarrValue / 10 * Ceiling));

	public RatingSource Source => RatingSource.RottenTomatoesAudience;

	public string Value => FormattableString.Invariant($"{Percent}%");

	public float SammlarrValue => Percent * 10 / Ceiling;

	public Optional<int> Votes => new None();

	public Optional<RatingVerdict> Verdict => Some.Of(Percent >= Upright ? RatingVerdict.Liked : RatingVerdict.Disliked);
}

public readonly record struct MetacriticRating(int Metascore) : IOwnedRating<MetacriticRating>
{
	private const int Favorable = 61;

	private const int Unfavorable = 39;

	public static MediaProvider Owner => MediaProvider.Metacritic;

	public static bool PublishesVotes => false;

	public static MetacriticRating Rate(float sammlarrValue, Optional<int> votes) => new((int)MathF.Round(sammlarrValue * 10));

	public RatingSource Source => RatingSource.Metacritic;

	public string Value => Metascore.ToString(CultureInfo.InvariantCulture);

	public float SammlarrValue => Metascore / 10f;

	public Optional<int> Votes => new None();

	public Optional<RatingVerdict> Verdict => VerdictOf(Metascore);

	private static Optional<RatingVerdict> VerdictOf(int metascore)
	{
		return metascore switch
		{
			>= Favorable => Some.Of(RatingVerdict.Liked),
			<= Unfavorable => Some.Of(RatingVerdict.Disliked),
			_ => new None(),
		};
	}
}

public readonly record struct LetterboxdRating(float Stars, Optional<int> Votes) : IOwnedRating<LetterboxdRating>
{
	public static MediaProvider Owner => MediaProvider.Letterboxd;

	public static bool PublishesVotes => true;

	public static LetterboxdRating Rate(float sammlarrValue, Optional<int> votes) => new(sammlarrValue / 2, votes);

	public RatingSource Source => RatingSource.Letterboxd;

	public string Value => Stars.ToString("0.0", CultureInfo.InvariantCulture);

	public float SammlarrValue => Stars * 2;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct TraktRating(int Percent, Optional<int> Votes) : IOwnedRating<TraktRating>
{
	public static MediaProvider Owner => MediaProvider.Trakt;

	public static bool PublishesVotes => true;

	public static TraktRating Rate(float sammlarrValue, Optional<int> votes) => new((int)MathF.Round(sammlarrValue * 10), votes);

	public RatingSource Source => RatingSource.Trakt;

	public string Value => FormattableString.Invariant($"{Percent}%");

	public float SammlarrValue => Percent / 10f;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct MyAnimeListRating(float Score, Optional<int> Votes) : IOwnedRating<MyAnimeListRating>
{
	public static MediaProvider Owner => MediaProvider.Mal;

	public static bool PublishesVotes => true;

	public static MyAnimeListRating Rate(float sammlarrValue, Optional<int> votes) => new(sammlarrValue, votes);

	public RatingSource Source => RatingSource.MyAnimeList;

	public string Value => Score.ToString("0.00", CultureInfo.InvariantCulture);

	public float SammlarrValue => Score;

	public Optional<RatingVerdict> Verdict => new None();
}

public readonly record struct AniListRating(int Score, Optional<int> Votes) : IOwnedRating<AniListRating>
{
	public static MediaProvider Owner => MediaProvider.AniList;

	public static bool PublishesVotes => true;

	public static AniListRating Rate(float sammlarrValue, Optional<int> votes) => new((int)MathF.Round(sammlarrValue * 10), votes);

	public RatingSource Source => RatingSource.AniList;

	public string Value => FormattableString.Invariant($"{Score}%");

	public float SammlarrValue => Score / 10f;

	public Optional<RatingVerdict> Verdict => new None();
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
