namespace Contracts.Auth;

public sealed record AuthPinResponse
{
	public int Id { get; init; }
	public DateTimeOffset ExpiresAt { get; set; }
	public string? AuthToken { get; init; }
}
