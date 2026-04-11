using System.ComponentModel.DataAnnotations;

namespace DB.Data.Watchlist;

internal record CollectionItemEntity : Entity<Guid>
{
	[Required]
	[MaxLength(200)]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }
}
