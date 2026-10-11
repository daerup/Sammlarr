namespace Shared.Concurrency;

public union AttemptOutcome(AttemptAnswered, AttemptThrottled, AttemptFailed, AttemptAbandoned);

public sealed record AttemptAnswered;

public sealed record AttemptThrottled(Optional<DateTimeOffset> RetryAt);

public sealed record AttemptFailed;

public sealed record AttemptAbandoned;
