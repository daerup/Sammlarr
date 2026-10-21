namespace UI.Features.Feeds.Pages.Editor.Filters;

internal static class FilterDepth
{
	public static string Stripe(int depth)
	{
		return depth switch
		{
			0 => "border-s-muted-foreground/75",
			1 => "border-s-blue-500",
			_ => "border-s-purple-500",
		};
	}

	public static string Tone(int depth)
	{
		return depth switch
		{
			0 => "text-muted-foreground",
			1 => "text-blue-600 dark:text-blue-400",
			_ => "text-purple-600 dark:text-purple-400",
		};
	}
}
