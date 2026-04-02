using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Contracts.Media;

namespace DB.Data.Entities;

[Table("ApiKeySettings")]
public record ApiKeySetting : Entity<Guid>
{
	[Required]
	public required MediaProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	public required string ApiKey { get; set; } = string.Empty;
}
