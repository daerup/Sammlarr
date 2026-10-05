using Domain.Providers;

namespace Domain.Media;

public sealed record CachedDetails(MediaDetails Details, DateTimeOffset RefreshedAt, DateTimeOffset AskedAt, ValueSet<ProviderAnswer> Answers)
{
	private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

	public ValueSet<MediaProvider> Answered => [.. Answers.Select(answer => answer.Provider)];

	public bool IsStale(DateTimeOffset now) => now - RefreshedAt >= StaleAfter;

	public CachedDetails Replacing(CachedDetails stored)
	{
		var heardFromEveryone = stored.Answers.Count > 0 && Answered.IsSupersetOf(stored.Answered);
		return heardFromEveryone ? this : LaidOver(stored);
	}

	private CachedDetails LaidOver(CachedDetails stored)
	{
		return this with { Details = Details.FillFrom(stored.Details), Answers = [.. Answers.UnionBy(stored.Answers, answer => answer.Provider)] };
	}
}
