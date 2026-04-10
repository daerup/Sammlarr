using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Auth;
using DB.Encryption;

namespace DB.Data.Entities;

[Table("Credentials")]
public record Credential : Entity<Guid>
{
	[Required]
	public required WatchlistProvider WatchlistProvider { get; set; }

	[Required]
	[Encrypted]
	public required Guid ClientId { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string AccessToken { get; set; } = string.Empty;

	public required DateTimeOffset ExpiresAt { get; set; }
}
