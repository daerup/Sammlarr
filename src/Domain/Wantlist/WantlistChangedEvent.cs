using Domain.Announcing;
using Domain.Identity;
using Domain.Media;

namespace Domain.Wantlist;

public sealed record WantlistChangedEvent(UserId UserId, IReadOnlyList<MediaId> Added, IReadOnlyList<MediaId> Removed) : IEvent;
