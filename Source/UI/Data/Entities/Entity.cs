using System;
using System.ComponentModel.DataAnnotations;

namespace UI.Data.Entities;

public abstract class Entity<TId> : IIdentifiable<TId>, IAuditable
{
	[Key]
	public TId Id { get; set; } = default!;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
