using Domain.Providers;

namespace Domain.Media;

public sealed record ProviderAnswer(RemoteService Provider, MediaFields Unfilled, DateTimeOffset AnsweredAt, int Failures = 0)
{
	private static readonly TimeSpan[] Backoff = [TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(24)];

	public DateTimeOffset AskableAt(TimeSpan freshFor) => AnsweredAt + Wait(freshFor);

	private TimeSpan Wait(TimeSpan freshFor) => Failures == 0 ? freshFor : Backoff[Math.Min(Failures, Backoff.Length) - 1];

	public static ProviderAnswer After(RemoteService provider, MediaFields fills, MediaDetails answer, DateTimeOffset at)
	{
		return new(provider, fills & answer.Missing, at);
	}

	public static ProviderAnswer Failed(RemoteService provider, Optional<ProviderAnswer> previous, DateTimeOffset at)
	{
		return new(provider, MediaFields.None, at, previous.Map(answer => answer.Failures).GetValueOr(0) + 1);
	}
}
