using Contracts.Media;
using Domain.Media;
using Domain.Usage;
using Observability.Usage;

namespace UI.Shared.Media;

internal static class MediaDetailsLoading
{
	extension(IMediaService media)
	{
		public async Task<IReadOnlyDictionary<MediaId, MatchedMedia>> GetDetailsByIdAsync(IEnumerable<MediaId> ids, CancellationToken cancellationToken = default)
		{
			using var marked = UsageCauses.Mark(UsageCause.ShowingTitles);
			var found = new Dictionary<MediaId, MatchedMedia>();
			await foreach (var item in media.GetDetailsAsync(ids, cancellationToken).WhereSuccessAwait())
			{
				found[item.Identity.Id] = item;
			}
			return found;
		}
	}
}
