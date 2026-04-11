using System.ComponentModel.DataAnnotations;

namespace DB.Data.Watchlist;

internal record CollectionItem : Entity<Guid>
{
	[Required]
	[MaxLength(200)]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }
}
