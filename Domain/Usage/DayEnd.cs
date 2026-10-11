namespace Domain.Usage;

public sealed record DayEnd(long Left, DateTimeOffset ResetAt, Optional<DateTimeOffset> ReserveReachedAt);
