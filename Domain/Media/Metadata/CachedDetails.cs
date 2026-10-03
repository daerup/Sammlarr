namespace Domain.Media.Metadata;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset FetchedAt, MediaField ConfirmedAbsent)
{
	private static readonly TimeSpan staleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan retryGapsAfter = TimeSpan.FromHours(1);

	public bool IsFresh(DateTimeOffset now)
	{
		var age = now - FetchedAt;
		return age < staleAfter && (Details.Missing.Except(ConfirmedAbsent) is MediaField.None || age < retryGapsAfter);
	}
}
