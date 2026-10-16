using Domain.Identity;
using Domain.Providers;

namespace Domain.Media;

public readonly union ConflictState(Undecided, Distinct);

public readonly record struct Undecided;

public sealed record Distinct(DistinctReason Reason, DateTimeOffset At);

public readonly union DistinctReason(Contradiction, TypesDiffer, AnsweredElsewhere, KeptApartByHand);

public sealed record Contradiction(MediaProvider Source);

public readonly record struct TypesDiffer;

public sealed record AnsweredElsewhere(RemoteService By, IIdentityKey Answer);

public sealed record KeptApartByHand(UserId By);
