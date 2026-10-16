using Domain.Providers;

namespace Domain.Media;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset RefreshedAt, DateTimeOffset AskedAt, ValueSet<ProviderAnswer> Answers)
{
	public bool IsStale(DateTimeOffset now, TimeSpan freshFor) => now - RefreshedAt >= freshFor;

	public ValueSet<RemoteService> Resting(DateTimeOffset now, TimeSpan freshFor)
	{
		return [.. Answers.Where(answer => answer.AskableAt(freshFor) > now).Select(answer => answer.Provider)];
	}
}
