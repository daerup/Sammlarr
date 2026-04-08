using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Media;
using DB.Encryption;

namespace DB.Data.Entities;

[Table("ApiKeySettings")]
public record ApiKeySetting : Entity<Guid>
{
	[Required]
	public required MediaProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	[Encrypted]
	public required string ApiKey { get; set; } = string.Empty;
}
