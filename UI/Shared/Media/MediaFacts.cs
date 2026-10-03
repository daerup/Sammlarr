using Domain.Media;

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

		var suffix = count == 1 ? string.Empty : "s";
		facts.Add($"{count} {singularLabel}{suffix}");
	}

	private static string? GetRuntimeLabel(TimeSpan runtime)
	{
		var totalMinutes = (int)Math.Round(runtime.TotalMinutes, MidpointRounding.AwayFromZero);
		if (totalMinutes <= 0)
		{
			return null;
		}

		var hours = totalMinutes / 60;
		var minutes = totalMinutes % 60;

		return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
	}
}
