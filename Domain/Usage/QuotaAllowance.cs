namespace Domain.Usage;

public sealed record QuotaAllowance(
	long Remaining,
	DateTimeOffset ResetAt,
	Optional<long> Allowance,
	Optional<long> Outside,
	Optional<DateTimeOffset> ReserveReachedAt,
	long Reserve,
	double BackgroundShare
);

public sealed record QuotaToday(DateTimeOffset NextReset, Optional<QuotaAllowance> Answered);
