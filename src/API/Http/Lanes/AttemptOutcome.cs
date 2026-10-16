namespace API.Http.Lanes;

internal readonly union AttemptOutcome(AttemptAnswered, AttemptThrottled, AttemptFailed, AttemptAbandoned);

internal sealed record AttemptAnswered;

internal sealed record AttemptThrottled(Optional<DateTimeOffset> RetryAt);

internal sealed record AttemptFailed;

internal sealed record AttemptAbandoned;
