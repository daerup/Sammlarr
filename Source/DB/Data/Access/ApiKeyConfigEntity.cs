using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Media;
using DB.Conversion.Encryption;
using DB.Conversion.Enums;

namespace DB.Data.Access;

[Table("ApiKeyConfigs")]
internal record ApiKeyConfigEntity : Entity<Guid>
{
	[Required]
	[EnumAsString]
	public required MediaProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string ApiKey { get; set; } = string.Empty;
}
