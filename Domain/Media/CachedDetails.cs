namespace Domain.Media;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset FetchedAt, MediaFields ConfirmedAbsent)
{
	private static readonly TimeSpan staleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan retryGapsAfter = TimeSpan.FromHours(1);

	public bool IsFresh(DateTimeOffset now)
	{
		var age = now - FetchedAt;
		return age < staleAfter && (Details.Missing.Except(ConfirmedAbsent).IsEmpty || age < retryGapsAfter);
	}
}
