namespace UI.Shared.Media;

internal static class WantlistNotices
{
	internal const string CantUpdate = "Can't update wantlist";

	internal static string NoMatch(string sink, string? title)
	{
		return $"{sink} has no match for '{title}', so it can't go on your wantlist.";
	}

	internal static string NoMatch(string sink, int count)
	{
		return count == 1
			? $"{sink} has no match for 1 title, so it can't go on your wantlist."
			: $"{sink} has no match for {count} titles, so they can't go on your wantlist.";
	}
}
