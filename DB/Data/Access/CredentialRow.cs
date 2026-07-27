using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DB.Conversion.Encryption;
using Domain.Providers;

namespace DB.Data.Access;

[Table("Credentials")]
internal record CredentialRow : Entity<Guid>, IUserOwned
{
	[Required]
	public required AuthProvider Provider { get; set; }

	[Required]
	[Encrypted]
	public required Guid ClientId { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string AccessToken { get; set; } = string.Empty;

	public required DateTimeOffset ExpiresAt { get; set; }
}
