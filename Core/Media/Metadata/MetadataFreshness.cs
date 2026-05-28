using Contracts;
using Contracts.Media.Search;

namespace Core.Media.Metadata;

internal static class MetadataFreshness
{
	private static readonly TimeSpan staleAfter = TimeSpan.FromDays(7);
	private static readonly TimeSpan hydrationCooldown = TimeSpan.FromHours(1);

	public static bool IsFresh(IMediaDetail detail, DateTimeOffset? fetchedAt, IDateTimeProvider clock)
	{
		if (fetchedAt is null)
		{
			return false;
		}
		var sinceFetched = clock.Now - fetchedAt;
		if (sinceFetched >= staleAfter)
		{
			return false;
		}
		return MetadataCompleteness.GetMissingFields(detail).Count == 0 || sinceFetched < hydrationCooldown;
	}
}
