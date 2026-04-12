using Contracts.Media.Search;

namespace Contracts.Media.List;

public interface IImportListService
{
	Task<IReadOnlyList<IMediaSearchResult>> GetEnrichedImportListItemsAsync(string listIdentifier, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default);
}
