using Domain.Providers;

namespace Domain.Media;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset RefreshedAt, DateTimeOffset AskedAt, ValueSet<ProviderAnswer> Answers)
{
	public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

	public bool IsStale(DateTimeOffset now) => now - RefreshedAt >= StaleAfter;

	public ValueSet<RemoteService> Resting(DateTimeOffset now) => [.. Answers.Where(answer => answer.AskableAt > now).Select(answer => answer.Provider)];
}
