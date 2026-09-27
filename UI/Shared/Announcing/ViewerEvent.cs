using Domain.Announcing;
using Domain.Identity;

namespace UI.Shared.Announcing;

internal static class ViewerEvent
{
	internal static TEvent? Own<TEvent>(IEvent raised, UserId? viewer)
		where TEvent : class, IEvent => viewer is { } user && raised.UserId == user && raised is TEvent own ? own : null;
}
