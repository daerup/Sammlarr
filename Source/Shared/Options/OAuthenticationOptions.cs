namespace Shared.Options;

public sealed record OAuthenticationOptions
{
	public required string ProductName { get; set; } = "Sammlarr";
}
