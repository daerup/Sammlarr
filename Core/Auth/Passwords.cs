namespace Core.Auth;

internal static class Passwords
{
	internal static Optional<Error> Mismatch(string password, string confirmation)
	{
		return Optional.When(password != confirmation, () => Error.From("The two passwords do not match."));
	}

	internal static Optional<Error> Problem(string password, string confirmation, int minimum)
	{
		return Mismatch(password, confirmation)
			.Or(() => Optional.When(password.Length < minimum, () => Error.From($"A password needs at least {minimum} characters.")));
	}
}
