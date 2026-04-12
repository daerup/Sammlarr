using Contracts.Media.List;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.List;

internal class ImportListRepository(SammlarrDbContext context) : IImportListRepository
{
	public async Task<IReadOnlyList<ImportList>> GetImportListsAsync(CancellationToken cancellationToken = default)
	{
		return await context
			.Lists.AsNoTracking()
			.Select(l => new ImportList
			{
				Id = l.Id,
				Name = l.Name,
				Enabled = l.IsEnabled,
				Provider = l.Provider,
				Strategy = l.Strategy,
				ListIdentifier = l.Identifier,
			})
			.ToListAsync(cancellationToken);
	}

	public async Task CreateImportListsAsync(string name, ImportListCreateData createData, CancellationToken cancellationToken = default)
	{
		var entity = new ImportListEntity
		{
			Name = name,
			IsEnabled = createData.IsEnabled,
			Provider = createData.Provider,
			Strategy = createData.Strategy,
			Identifier = createData.ListIdentifier,
		};

		context.Lists.Add(entity);
		await context.SaveChangesAsync(cancellationToken);
	}
}
