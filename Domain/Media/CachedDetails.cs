using Domain.Providers;

namespace Domain.Media;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset RefreshedAt, DateTimeOffset AskedAt, ValueSet<ProviderAnswer> Answers)
{
	public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

	public ValueSet<MediaProvider> Answered => [.. Answers.Select(answer => answer.Provider)];

	public bool IsStale(DateTimeOffset now) => now - RefreshedAt >= StaleAfter;
}
