using Domain.Providers;

namespace Domain.Media;

public sealed record IdentityConflict(
	MediaId Held,
	MediaId Other,
	IIdentityKey Claimed,
	ValueSet<RemoteService> Sources,
	DateTimeOffset SeenAt,
	int Attempts,
	DateTimeOffset AskAfter,
	Optional<LastAsk> LastAsk,
	ConflictState State
)
{
	public IdentityConflict Asked(LastAsk ask) => this with { Attempts = Attempts + 1, LastAsk = Some.Of(ask), AskAfter = ask.At + Backoff(Attempts + 1) };

	public static Optional<DistinctReason> Contradicts(ValueSet<IIdentityKey> held, ValueSet<IIdentityKey> other)
	{
		var shared = held.Select(key => key.Source).Intersect(other.Select(key => key.Source)).Order().FirstOrNone();
		return shared
			.Map(source => (DistinctReason)new Contradiction(source))
			.Or(() => Optional.When(held.Concat(other).Select(key => key.Type).Distinct().Count() > 1, () => (DistinctReason)new TypesDiffer()));
	}

	private static TimeSpan Backoff(int attempts)
	{
		return attempts switch
		{
			1 => TimeSpan.FromDays(1),
			2 => TimeSpan.FromDays(7),
			_ => TimeSpan.FromDays(30),
		};
	}
}

public sealed record LastAsk(RemoteService Service, AskOutcome Outcome, DateTimeOffset At);

public enum AskOutcome
{
	NoMatch,
	Unavailable,
	Faulted,
}
