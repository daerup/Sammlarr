using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UI.Data.Stores;

public interface IWatchlistStore
{
	Task<IReadOnlyList<PlexWatchlistItem>> GetWatchlistAsync(CancellationToken cancellationToken = default);

	Task<bool> AddToWatchlistAsync(string ratingKey, CancellationToken cancellationToken = default);
}
