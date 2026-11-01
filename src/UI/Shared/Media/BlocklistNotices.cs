namespace UI.Shared.Media;

internal static class BlocklistNotices
{
	internal const string CantUpdate = "Can't update blocklist";

	internal static string NotReady(string? title)
	{
		return $"'{title}' isn't ready yet: still loading.";
	}
}
