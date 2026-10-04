using System.ComponentModel.DataAnnotations;
using Domain.Identity;

namespace DB.Data.Identity;

internal sealed record AccountLinkRow : Entity<Guid>
{
	public required AccountLinkKind Kind { get; set; }

	[MaxLength(128)]
	public required string Username { get; set; }

	[MaxLength(128)]
	public required string DisplayName { get; set; }

	public Guid? UserId { get; set; }

	[MaxLength(64)]
	public required string TokenHash { get; set; }

	[MaxLength(128)]
	public required string IssuedBy { get; set; }

	public required DateTimeOffset ExpiresAt { get; set; }
}
