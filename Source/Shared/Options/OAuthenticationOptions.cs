namespace Shared.Options;

public record OAuthenticationOptions
{
	public required string ProductName { get; set; } = "Sammlarr";
}
