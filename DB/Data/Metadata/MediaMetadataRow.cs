using System.ComponentModel.DataAnnotations;
using Contracts.Media;
using DB.Data.Media;

namespace DB.Data.Metadata;

internal abstract record MediaMetadataRow : IAuditable
{
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public DateTimeOffset LastFetchedAt { get; set; }

	[Key]
	public Guid MediaId { get; set; }

#pragma warning disable CS8618
	public MediaRow Media { get; set; }
#pragma warning restore CS8618

	[Required]
	[MaxLength(500)]
	public string Title { get; set; } = string.Empty;

	[Required]
	public string Tagline { get; set; } = string.Empty;

	[Required]
	public string Summary { get; set; } = string.Empty;

	[Required]
	public string PosterUrl { get; set; } = string.Empty;

	[Required]
	public string BackdropUrl { get; set; } = string.Empty;

	[Required]
	public DateOnly ReleaseDate { get; set; }

	[Required]
	public List<string> Genres { get; set; } = [];

	[Required]
	public MediaType MediaType { get; init; }

	public List<MediaRatingRow> Ratings { get; set; } = [];
}

internal record MovieMetadataRow : MediaMetadataRow
{
	public MovieMetadataRow()
	{
		MediaType = MediaType.Movie;
	}

	[Required]
	public required TimeSpan Runtime { get; set; }
}

internal record ShowMetadataRow : MediaMetadataRow
{
	public ShowMetadataRow()
	{
		MediaType = MediaType.Show;
	}

	[Required]
	public required int Seasons { get; set; }

	[Required]
	public required int Episodes { get; set; }

	[Required]
	public required TimeSpan RuntimePerEpisode { get; set; }
}
