using Contracts.Media.List;
using Contracts.Media.Search;

namespace API.Mdblist;

internal class MdblistService : IImportListService
{
	public Task<IReadOnlyList<IMediaSearchResult>> GetEnrichedImportListItemsAsync(string listIdentifier, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();

	public Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default) =>
		throw new NotImplementedException();
}
