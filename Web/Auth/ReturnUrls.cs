namespace Web.Auth;

internal static class ReturnUrls
{
	internal static string Target(string? returnUrl) => returnUrl is { } url && IsLocal(url) ? url : "/";

	private static bool IsLocal(string url)
	{
		return url.StartsWith('/') && (url.Length == 1 || url[1] is not ('/' or '\\')) && !url.Any(char.IsControl);
	}
}
