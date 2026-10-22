using Shared.Options;

namespace UI.Features.Feeds.Pages.Editor.Filters;

internal static class FilterDepth
{
	public static string Stripe(this FeedFilterOptions options, int depth) => options.Levels[depth].Stripe;

	public static string Tone(this FeedFilterOptions options, int depth) => options.Levels[depth].Tone;
}
