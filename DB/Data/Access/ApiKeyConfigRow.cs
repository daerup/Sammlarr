using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DB.Conversion.Encryption;

namespace DB.Data.Access;

[Table("ApiKeyConfigs")]
internal record ApiKeyConfigRow : Entity<Guid>
{
	[Required]
	public required ApiKeyProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string ApiKey { get; set; } = string.Empty;
}
