using Domain.Media;

namespace UI.Shared.Media;

internal static class MediaFacts
{
	public static IReadOnlyList<string> GetDisplayFacts(MediaLookup media)
	{
		var facts = new List<string>();

		if (media.MediaType is MediaType.Show)
		{
			AddCountFact(facts, media.Seasons ?? 0, "season");
			AddCountFact(facts, media.Episodes ?? 0, "episode");
		}
		else if (media.MediaType is MediaType.Movie && media.Runtime is { } runtime)
		{
			var label = GetRuntimeLabel(runtime);
			if (!string.IsNullOrWhiteSpace(label))
			{
				facts.Add(label);
			}
		}

		return facts;
	}

	private static void AddCountFact(ICollection<string> facts, int count, string singularLabel)
	{
		if (count <= 0)
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
