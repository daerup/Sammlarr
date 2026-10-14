using Microsoft.AspNetCore.Identity;

namespace Core.Auth;

internal static class Passwords
{
	private static readonly PasswordHasher<string> Hasher = new();

	internal static readonly string Decoy = Hash(Guid.NewGuid().ToString());

	internal static string Hash(string password) => Hasher.HashPassword(string.Empty, password);

	internal static bool Verify(string hash, string password) => Hasher.VerifyHashedPassword(string.Empty, hash, password) != PasswordVerificationResult.Failed;

	internal static Optional<Error> Weakness(string password, int minimum)
	{
		return Optional.When(password.Length < minimum, () => Error.From(FormattableString.CurrentCulture($"A password needs at least {minimum} characters.")));
	}

	internal static Optional<Error> Problem(string password, string confirmation, int minimum)
	{
		return Optional.When(password != confirmation, () => Error.From("The two passwords do not match.")).Or(() => Weakness(password, minimum));
	}
}
