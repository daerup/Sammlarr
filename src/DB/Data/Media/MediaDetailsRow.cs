using System.ComponentModel.DataAnnotations;
using Domain.Media;

namespace DB.Data.Media;

internal abstract record MediaDetailsRow : IAuditable
{
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public DateTimeOffset RefreshedAt { get; set; }
	public DateTimeOffset AskedAt { get; set; }

	[Key]
	public Guid MediaId { get; set; }

#pragma warning disable CS8618
	public MediaRow Media { get; set; }
#pragma warning restore CS8618

	[MaxLength(500)]
	public string? Title { get; set; }

	public string? Tagline { get; set; }

	public string? Summary { get; set; }

	public string? PosterUrl { get; set; }

	public string? BackdropUrl { get; set; }

	public DateOnly? ReleaseDate { get; set; }

	[Required]
	public List<string> Genres { get; set; } = [];

	[Required]
	public MediaType MediaType { get; init; }

	public List<MediaRatingRow> Ratings { get; set; } = [];

	public List<ProviderAnswerRow> Answers { get; set; } = [];

	public List<MediaPageLinkRow> PageLinks { get; set; } = [];
}

internal sealed record MovieDetailsRow : MediaDetailsRow
{
	public MovieDetailsRow()
	{
		MediaType = MediaType.Movie;
	}

	public TimeSpan? Runtime { get; set; }
}

internal sealed record ShowDetailsRow : MediaDetailsRow
{
	public ShowDetailsRow()
	{
		MediaType = MediaType.Show;
	}

	public int? Seasons { get; set; }

	public int? Episodes { get; set; }

	public TimeSpan? RuntimePerEpisode { get; set; }
}
