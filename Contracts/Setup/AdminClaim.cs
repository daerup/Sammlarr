namespace Contracts.Setup;

public sealed record AdminClaim(
	[property: Sensitive] string SetupCode,
	string Username,
	[property: Sensitive] string Password,
	[property: Sensitive] string Confirmation
);
