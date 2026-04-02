using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Auth;

namespace DB.Data.Entities;

[Table("PlexCredentials")]
public record Credential : Entity<Guid>
{
	[Required]
	public required AuthProvider AuthProvider { get; set; }

	[Required]
	public required Guid ClientId { get; set; }

	[Required]
	[MaxLength(4096)]
	public required string AccessToken { get; set; } = string.Empty;

	public required DateTimeOffset ExpiresAt { get; set; }
}
