namespace Contracts.Media.List;

public interface IImportListService
{
    Task<IReadOnlyList<ImportListItem>> GetAllImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportListItem>> GetDistilledImportListItemsAsync(ImportList list, CancellationToken cancellationToken = default);
}