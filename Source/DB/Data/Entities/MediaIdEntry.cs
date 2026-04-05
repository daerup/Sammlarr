using System.ComponentModel.DataAnnotations;
using Contracts.Media;

namespace DB.Data.Entities;

public record MediaIdEntry : Entity<Guid>
{
	[Required]
	[MaxLength(64)]
	public required MediaProvider Source { get; set; }

	[Required]
	[MaxLength(64)]
	public required string ExternalId { get; set; }

	[Required]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	public MediaIdGroup Group { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
