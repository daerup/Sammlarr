using System;
using System.ComponentModel.DataAnnotations;

namespace UI.Data.Entities;

public sealed class PlexCredential : Entity<Guid>
{
	[Required]
	public string AccessToken { get; set; } = string.Empty;

	public DateTimeOffset? AccessTokenExpiresAt { get; set; }

	[Required]
	[MaxLength(1024)]
	public string RefreshToken { get; set; } = string.Empty;
}
