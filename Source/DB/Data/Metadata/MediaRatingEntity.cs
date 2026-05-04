using System.ComponentModel.DataAnnotations;
using Contracts.Media.Metadata;
using DB.Conversion.Enums;

namespace DB.Data.Metadata;

internal record MediaRatingEntity
{
	[Required]
	[EnumAsString]
	public required RatingSource Source { get; set; }

	[Required]
	public required float NormalizedValue { get; set; }

	public int? VoteCount { get; set; }

#pragma warning disable CS8618
	public MediaMetadataEntity Metadata { get; set; }
#pragma warning restore CS8618
}
