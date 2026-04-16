using Contracts.Media.List;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.List;

internal class ImportListRepository(SammlarrDbContext context) : IImportListRepository
{
	public async Task<IReadOnlyList<ImportList>> GetImportListsAsync(CancellationToken cancellationToken = default)
	{
		return await context
			.Lists.AsNoTracking()
			.OrderByDescending(l => l.CreatedAt)
			.Select(l => new ImportList
			{
				Id = l.Id,
				Name = l.Name,
				Enabled = l.IsEnabled,
				Provider = l.Provider,
				Strategy = l.Strategy,
				ListIdentifier = l.Identifier,
				CreatedAt = l.CreatedAt,
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

	public async Task CreateImportListsAsync(Guid id, ImportListUpdateData updateData, CancellationToken cancellationToken = default)
	{
		var entity = await context.Lists.FindAsync([id], cancellationToken: cancellationToken);
		if (entity == null)
			throw new InvalidOperationException($"Import list with id {id} not found.");
		entity.IsEnabled = updateData.IsEnabled;
		entity.Provider = updateData.Provider;
		entity.Strategy = updateData.Strategy;
		entity.Identifier = updateData.ListIdentifier;
		entity.Name = updateData.Name;
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteImportListAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var entity = await context.Lists.FindAsync([id], cancellationToken: cancellationToken);
		if (entity == null)
			return;
		context.Lists.Remove(entity);
		await context.SaveChangesAsync(cancellationToken);
	}
}
