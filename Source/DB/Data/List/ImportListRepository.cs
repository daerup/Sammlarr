using System.Linq.Expressions;
using Contracts.Media.List;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.List;

internal class ImportListRepository(SammlarrDbContext context) : IImportListRepository
{
	private static readonly Expression<Func<ImportListEntity, ImportList>> project = projection => new ImportList
	{
		Id = projection.Id,
		Name = projection.Name,
		Enabled = projection.IsEnabled,
		Provider = projection.Provider,
		Strategy = projection.Strategy,
		ListIdentifier = projection.Identifier,
		CreatedAt = projection.CreatedAt,
		Filter = projection.Filter,
		Sort = projection.Sort,
		Limit = projection.Limit,
		Schedule = projection.SyncMode,
	};

	public async Task<IReadOnlyList<ImportList>> GetImportListsAsync(CancellationToken cancellationToken = default) =>
		await context.Lists.AsNoTracking().OrderByDescending(l => l.CreatedAt).Select(project).ToListAsync(cancellationToken);

	public async Task<ImportList?> GetImportListByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await context.Lists.AsNoTracking().Where(l => l.Id == id).Select(project).FirstOrDefaultAsync(cancellationToken);

	public async Task CreateImportListsAsync(string name, ImportListCreateData createData, CancellationToken cancellationToken = default)
	{
		var entity = new ImportListEntity
		{
			Name = name,
			IsEnabled = createData.IsEnabled,
			Provider = createData.Provider,
			Strategy = createData.Strategy,
			Identifier = createData.ListIdentifier,
			Filter = createData.Filter,
			Sort = createData.Sort,
			Limit = createData.Limit,
			SyncMode = createData.Schedule,
		};

		context.Lists.Add(entity);
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task UpdateImportListsAsync(Guid id, ImportListUpdateData updateData, CancellationToken cancellationToken = default)
	{
		var entity = await context.Lists.FindAsync([id], cancellationToken);
		if (entity == null)
		{
			throw new InvalidOperationException($"Import list with id {id} not found.");
		}

		entity.Name = updateData.Name;
		entity.IsEnabled = updateData.IsEnabled;
		entity.Provider = updateData.Provider;
		entity.Strategy = updateData.Strategy;
		entity.Identifier = updateData.ListIdentifier;
		entity.Filter = updateData.Filter;
		entity.Sort = updateData.Sort;
		entity.Limit = updateData.Limit;
		entity.SyncMode = updateData.Schedule;

		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteImportListAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var entity = await context.Lists.FindAsync([id], cancellationToken);
		if (entity == null)
		{
			return;
		}
		context.Lists.Remove(entity);
		await context.SaveChangesAsync(cancellationToken);
	}
}
