using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Media;
using DB.Conversion.Encryption;

namespace DB.Data.Access;

[Table("ApiKeyConfigs")]
internal record ApiKeyConfigEntity : Entity<Guid>
{
	[Required]
	public required MediaProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string ApiKey { get; set; } = string.Empty;
}
