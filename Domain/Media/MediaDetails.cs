namespace Domain.Media;

public closed record MediaDetails
{
	public abstract MediaType Type { get; }
	public string? Title { get; init; }
	public string? Tagline { get; init; }
	public string? Summary { get; init; }
	public string? PosterUrl { get; init; }
	public string? BackdropUrl { get; init; }
	public DateOnly? ReleaseDate { get; init; }
	public ValueSet<string> Genres { get; init; } = [];
	public RatingSummary Rating { get; init; } = RatingSummary.None;

	public static MediaDetails Empty(MediaType type)
	{
		return type switch
		{
			MediaType.Movie => new MovieDetails(),
			MediaType.Show => new ShowDetails(),
			MediaType.Unknown => new UnknownDetails(),
		};
	}
}

public sealed record MovieDetails : MediaDetails
{
	public override MediaType Type => MediaType.Movie;
	public TimeSpan? Runtime { get; init; }
}

public sealed record ShowDetails : MediaDetails
{
	public override MediaType Type => MediaType.Show;
	public int? Seasons { get; init; }
	public int? Episodes { get; init; }
	public TimeSpan? RuntimePerEpisode { get; init; }
}

public sealed record UnknownDetails : MediaDetails
{
	public override MediaType Type => MediaType.Unknown;
}
