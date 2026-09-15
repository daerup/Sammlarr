namespace Domain.Media.Metadata;

public static class MediaFreshness
{
	private static readonly TimeSpan staleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan hydrationCooldown = TimeSpan.FromHours(1);
	private static readonly string[] hydratableFields = [nameof(IPartialMedia.Tagline), nameof(IPartialMedia.BackdropUrl)];

	public static bool IsFresh(IdentifiedMedia stored, DateTimeOffset? fetchedAt, DateTimeOffset now)
	{
		if (fetchedAt is null)
		{
			return false;
		}
		var sinceFetched = now - fetchedAt.Value;
		if (sinceFetched >= staleAfter)
		{
			return false;
		}
		return stored is IMedia || LacksOnlyHydratableFields(stored) || sinceFetched < hydrationCooldown;
	}

	private static bool LacksOnlyHydratableFields(IdentifiedMedia stored) =>
		stored is IPartialMedia partial && partial.MissingFields.All(hydratableFields.Contains);
}
