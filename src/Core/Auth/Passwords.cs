namespace Core.Auth;

internal static class Passwords
{
	internal static Optional<Error> Mismatch(Secret password, Secret confirmation)
	{
		return Optional.When(password != confirmation, () => Error.From("The two passwords do not match."));
	}
}
