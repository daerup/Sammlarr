using Domain.Media;

namespace Contracts.Wantlist;

public sealed record WantlistAdded(IReadOnlyList<MediaId> Added, IReadOnlyList<MediaId> Unaddressable);
