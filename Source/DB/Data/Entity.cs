using System.ComponentModel.DataAnnotations;

namespace DB.Data;

public abstract record Entity<TId> : IIdentifiable<TId>, IAuditable
{
	[Key]
	public TId Id { get; set; } = default!;

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
