using System.Diagnostics;
using Contracts.Media;

namespace Core.Media;

internal readonly record struct TracedRequest(IdentifyRequest Request, Optional<Activity> Title)
{
	internal static TracedRequest Untraced(IdentifyRequest request) => new(request, new None());
}
