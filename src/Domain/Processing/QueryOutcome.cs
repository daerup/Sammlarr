namespace Domain.Processing;

public readonly union QueryOutcome(Qualified, Culled, Truncated);

public readonly record struct Qualified(bool ConsumesSlot, Optional<SlotRank> Rank);

public readonly record struct Culled(FilterChecks Checks);

public readonly record struct Truncated(SlotRank Rank);

public readonly record struct SlotRank(int Position, int Slots);
