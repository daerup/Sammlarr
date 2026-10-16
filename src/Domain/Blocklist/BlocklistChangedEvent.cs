using Domain.Announcing;
using Domain.Identity;
using Domain.Media;

namespace Domain.Blocklist;

public sealed record BlocklistChangedEvent(UserId UserId, IReadOnlyList<MediaId> Added, IReadOnlyList<MediaId> Removed) : IEvent;
