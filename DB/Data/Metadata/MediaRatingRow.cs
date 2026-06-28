using System.ComponentModel.DataAnnotations;
using Contracts.Media.Metadata;

namespace DB.Data.Metadata;

internal record MediaRatingRow
{
	[Required]
	public required RatingSource Source { get; set; }

	[Required]
	public required float NormalizedValue { get; set; }

	public int? VoteCount { get; set; }

#pragma warning disable CS8618
	public MediaMetadataRow Metadata { get; set; }
#pragma warning restore CS8618
}
