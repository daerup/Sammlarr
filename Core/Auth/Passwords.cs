using Microsoft.AspNetCore.Identity;

namespace Core.Auth;

internal static class Passwords
{
	internal const int MinimumLength = 12;

	private static readonly PasswordHasher<string> Hasher = new();

	internal static readonly string Decoy = Hash(Guid.NewGuid().ToString());

	internal static string Hash(string password) => Hasher.HashPassword(string.Empty, password);

	internal static bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(string.Empty, hash, password) != PasswordVerificationResult.Failed;

	internal static Optional<Error> Weakness(string password)
	{
		return Optional.When(password.Length < MinimumLength, () => Error.From($"A password needs at least {MinimumLength} characters."));
	}
}
