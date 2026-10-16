using Domain.Media;

namespace Contracts.Media;

public interface IConflictReview
{
	bool WaitsForAJob { get; }

	Task<IReadOnlyList<StoredTwice>> PairsAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Refused>> ApplyAsync(IReadOnlyList<ReviewChoice> choices, CancellationToken cancellationToken = default);
}

public sealed record StoredTwice(IdentityConflict Note, StoredTitle Held, StoredTitle Other);

public sealed record StoredTitle(StoredMedia Media, TitleUses Uses);

public readonly record struct TitleUses(int Wantlists, int Feeds);

public sealed record ReviewChoice(IdentityConflict Note, ConflictChoice Choice);

public enum ConflictChoice
{
	Merge,
	KeepApart,
	Leave,
}

public sealed record Refused(IdentityConflict Note, DistinctReason Reason);
