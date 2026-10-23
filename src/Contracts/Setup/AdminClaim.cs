namespace Contracts.Setup;

public sealed record AdminClaim(Secret SetupCode, string Username, Secret Password, Secret Confirmation);
