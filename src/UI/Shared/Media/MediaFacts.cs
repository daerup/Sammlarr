using Domain.Media;
using Humanizer;
using UI.Shared.Formatting;

namespace UI.Shared.Media;

internal static class MediaFacts
{
	public static IReadOnlyList<string> GetDisplayFacts(MediaDetails details)
	{
		var facts = new List<string>();
		switch (details)
		{
			case ShowDetails show:
				AddCountFact(facts, show.Seasons, "season");
				AddCountFact(facts, show.Episodes, "episode");
				break;
			case MovieDetails { Runtime: { } runtime } when GetRuntimeLabel(runtime) is { } label:
				facts.Add(label);
				break;
		}

		return facts;
	}

	private static void AddCountFact(ICollection<string> facts, int? count, string singularLabel)
	{
		if (count is not > 0)
		{
			return;
		}

		facts.Add(singularLabel.ToQuantity(count.Value));
	}

	private static string? GetRuntimeLabel(TimeSpan runtime)
	{
		var minutes = TimeSpan.FromMinutes(Math.Round(runtime.TotalMinutes, MidpointRounding.AwayFromZero));
		return minutes > TimeSpan.Zero ? minutes.Humanize(2, Notation.Symbols, TimeUnit.Minute) : null;
	}
}
