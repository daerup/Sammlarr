using Contracts.Media.Search;

namespace Contracts.Media.List;

public interface IImportListService
{
	IAsyncEnumerable<IMediaDetailResult> GetImportListItemsAsync(string listIdentifier, CancellationToken cancellationToken = default);
}
