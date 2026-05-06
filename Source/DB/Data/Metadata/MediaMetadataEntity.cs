using System.ComponentModel.DataAnnotations;
using Contracts.Media;
using DB.Conversion.Enums;
using DB.Data.MediaGroup;

namespace DB.Data.Metadata;

internal abstract record MediaMetadataEntity : IAuditable
{
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }

	[Key]
	public Guid MediaIdGroupId { get; set; }

#pragma warning disable CS8618
	public MediaIdentifierGroupEntity MediaIdGroup { get; set; }
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
	[EnumAsString]
	public MediaType MediaType { get; init; }

	public List<MediaRatingEntity> Ratings { get; set; } = [];
}

internal record MovieMetadataEntity : MediaMetadataEntity
{
	public MovieMetadataEntity()
	{
		MediaType = MediaType.Movie;
	}

	[Required]
	public required TimeSpan Runtime { get; set; }
}

internal record ShowMetadataEntity : MediaMetadataEntity
{
	public ShowMetadataEntity()
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
