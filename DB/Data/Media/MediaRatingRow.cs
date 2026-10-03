using System.ComponentModel.DataAnnotations;
using Domain.Providers;

namespace DB.Data.Media;

internal record MediaRatingRow
{
	[Required]
	public required MediaProvider Source { get; set; }

	[Required]
	public required float NormalizedValue { get; set; }

	public int? VoteCount { get; set; }

#pragma warning disable CS8618
	public MediaDetailsRow Details { get; set; }
#pragma warning restore CS8618
}
