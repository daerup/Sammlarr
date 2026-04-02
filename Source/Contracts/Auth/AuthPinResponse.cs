namespace Contracts.Auth;

public record AuthPinResponse
{
	public int Id { get; init; }
	public DateTimeOffset ExpiresAt { get; set; }
	public string? AuthToken { get; init; }
}
