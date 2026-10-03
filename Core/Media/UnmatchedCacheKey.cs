using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal static class UnmatchedCacheKey
{
	public static string Build(IEnumerable<IExternalMediaId> candidates, MediaProvider target)
	{
		var sortedIds = candidates.Select(id => id.ToString()).OrderBy(id => id, StringComparer.Ordinal);
		return $"{target}|{string.Join(",", sortedIds)}";
	}
}
