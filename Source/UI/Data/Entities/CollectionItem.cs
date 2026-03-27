using System;
using System.ComponentModel.DataAnnotations;

namespace UI.Data.Entities;

public sealed class CollectionItem : Entity<Guid>
{
	[Required]
	[MaxLength(200)]
	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }
}
