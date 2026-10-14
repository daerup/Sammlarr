using System.ComponentModel.DataAnnotations;

namespace DB.Data;

internal abstract record Entity<TId> : IAuditable
{
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }

	[Key]
	public TId Id { get; set; } = default!;
}
