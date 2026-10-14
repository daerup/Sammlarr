using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DB.Conversion.Encryption;
using Domain.Providers;

namespace DB.Data.Auth;

[Table("ApiKeyConfigs")]
internal sealed record ApiKeyConfigRow : Entity<Guid>, IUserOwned
{
	[Required]
	public required ApiKeyProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string ApiKey { get; set; } = string.Empty;
}
