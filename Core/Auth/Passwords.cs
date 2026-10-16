namespace Core.Auth;

internal static class Passwords
{
	internal static Optional<Error> Mismatch(string password, string confirmation)
	{
		return Optional.When(password != confirmation, () => Error.From("The two passwords do not match."));
	}
}
