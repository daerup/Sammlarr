using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Auth;
using DB.Conversion.Encryption;
using DB.Conversion.Enums;

namespace DB.Data.Access;

[Table("Credentials")]
internal record Credential : Entity<Guid>
{
	[Required]
	[EnumAsString]
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
