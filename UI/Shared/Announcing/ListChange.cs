using Domain.Media;

namespace UI.Shared.Announcing;

internal static class ListChange
{
	internal static bool Differs(Func<MediaId, bool> shows, IReadOnlyList<MediaId> added, IReadOnlyList<MediaId> removed)
	{
		return added.Any(id => !shows(id)) || removed.Any(shows);
	}
}
