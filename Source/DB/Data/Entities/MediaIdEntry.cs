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

	public MediaIdGroup Group { get; set; }
}
