using System.Diagnostics;
using Contracts.Media;
using Shared.Concurrency;

namespace Core.Media;

internal readonly record struct TracedRequest(IdentifyRequest Request, Optional<Activity> Title, LanePriority Priority)
{
	internal static TracedRequest Untraced(IdentifyRequest request) => new(request, new None(), LanePriority.Current);
}
