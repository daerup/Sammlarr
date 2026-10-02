namespace Domain.Media.Metadata;

public static class MediaFreshness
{
	private static readonly TimeSpan staleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan hydrationCooldown = TimeSpan.FromHours(1);

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
		return stored is IMedia || LacksOnlyConfirmedAbsentFields(stored) || sinceFetched < hydrationCooldown;
	}

	private static bool LacksOnlyConfirmedAbsentFields(IdentifiedMedia stored) =>
		stored is IPartialMedia partial && partial.Missing.Except(partial.ConfirmedAbsent) is MediaField.None;
}
