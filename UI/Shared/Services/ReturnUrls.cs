namespace UI.Shared.Services;

public static class ReturnUrls
{
	public static string Target(string? returnUrl) => returnUrl is { } url && IsLocal(url) ? url : "/";

	private static bool IsLocal(string url)
	{
		return url.StartsWith('/', StringComparison.Ordinal) && (url.Length == 1 || url[1] is not ('/' or '\\')) && !url.Any(char.IsControl);
	}
}
