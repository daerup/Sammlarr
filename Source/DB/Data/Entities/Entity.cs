using System.ComponentModel.DataAnnotations;

namespace DB.Data.Entities;

public abstract record Entity<TId> : IIdentifiable<TId>, IAuditable
{
	[Key]
	public TId Id { get; set; } = default!;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
