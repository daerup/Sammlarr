using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;

namespace DB.Data.Auth;

[Table("ApiKeyConfigs")]
internal sealed record ApiKeyConfigRow : Entity<Guid>, IUserOwned
{
	[Required]
	public required ApiKeyProvider Provider { get; set; }

	[Required]
	[MaxLength(4096)]
	public required Secret ApiKey { get; set; }
}
