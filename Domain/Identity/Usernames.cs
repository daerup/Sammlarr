namespace Domain.Identity;

public static class Usernames
{
	public static string Normalize(string username) => username.Trim().ToLowerInvariant();
}
