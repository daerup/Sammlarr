using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;

namespace DB.Data.Auth;

[Table("Credentials")]
internal sealed record CredentialRow : Entity<Guid>, IUserOwned
{
	[Required]
	public required AuthProvider Provider { get; set; }

	[Required]
	public required Guid ClientId { get; set; }

	[Required]
	[MaxLength(4096)]
	public required Secret AccessToken { get; set; }

	public required DateTimeOffset ExpiresAt { get; set; }
}
