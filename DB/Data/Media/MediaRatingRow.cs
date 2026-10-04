using System.ComponentModel.DataAnnotations;
using Domain.Media;

namespace DB.Data.Media;

internal record MediaRatingRow
{
	[Required]
	public required RatingSource Source { get; set; }

	[Required]
	public required float NormalizedValue { get; set; }

	public int? VoteCount { get; set; }

	[MaxLength(256)]
	public string? RottenTomatoesPath { get; set; }

	public DateTimeOffset? RottenTomatoesAskedAt { get; set; }

#pragma warning disable CS8618
	public MediaDetailsRow Details { get; set; }
#pragma warning restore CS8618
}
