namespace Contracts.Media.List;

public interface IImportListRepository
{
	Task<IReadOnlyList<ImportList>> GetImportListsAsync(CancellationToken cancellationToken = default);
	Task CreateImportListsAsync(string name, ImportListCreateData createData, CancellationToken cancellationToken = default);
}
