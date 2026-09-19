using Domain.Media;

namespace Domain.Feed.Run;

public sealed class FeedOwnership
{
	private readonly IReadOnlySet<MediaId> owned;

	public FeedOwnership(IReadOnlySet<MediaId> owned, IReadOnlySet<MediaId> blacklisted)
	{
		this.owned = owned;
		Active = [.. owned.Where(id => !blacklisted.Contains(id))];
		Waived = [.. owned.Where(blacklisted.Contains)];
	}

	public IReadOnlyList<MediaId> Active { get; }

	public IReadOnlyList<MediaId> Waived { get; }

	public bool Owns(MediaId id) => owned.Contains(id);
}
