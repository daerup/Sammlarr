namespace Domain.Identity;

public static class Usernames
{
	private const int MaximumLength = 64;

	public static string Normalize(string username) => username.Trim().ToLowerInvariant();

	public static Optional<Error> Problem(string username)
	{
		var trimmed = username.Trim();
		return Optional
			.When(trimmed.Length == 0, () => Error.From("A username cannot be empty."))
			.Or(trimmed.Length > MaximumLength, () => Error.From($"A username has at most {MaximumLength} characters."))
			.Or(trimmed.Any(char.IsWhiteSpace), () => Error.From("A username cannot contain spaces."));
	}
}
