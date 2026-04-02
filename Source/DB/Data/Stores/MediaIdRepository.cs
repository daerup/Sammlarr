using System.Linq.Expressions;
using Contracts.Media;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

public class MediaIdRepository(SammlarrDbContext dbContext) : IMediaIdStore
{
	public async Task<List<IMediaId>> GetByIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return await dbContext
			.MediaIds.Where(e => e.Group.MediaIds.AsQueryable().Any(MatchesId(mediaId)))
			.Select(e => e.MapValueObject())
			.ToListAsync(cancellationToken);
	}

	public async Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.DistinctBy(id => (id.Source, id.ToString())).ToList();
		foreach (var id in idList)
		{
			if (await dbContext.MediaIds.AnyAsync(MatchesId(id), cancellationToken))
				throw new InvalidOperationException("A group containing one of the provided IDs already exists");
		}
		dbContext.MediaIdGroups.Add(new MediaIdGroup { MediaIds = idList.Select(id => id.MapEntity()).ToList() });

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task AppendAsync(IMediaId existingId, IEnumerable<IMediaId> newIds, CancellationToken cancellationToken = default)
	{
		var group = (
			await dbContext.MediaIds.Include(e => e.Group).ThenInclude(g => g.MediaIds).SingleOrDefaultAsync(MatchesId(existingId), cancellationToken)
		)?.Group;

		if (group is null)
			throw new InvalidOperationException("No group found for the provided ID");

		var toAdd = newIds.Except(group.MediaIds.Select(e => e.MapValueObject())).Distinct().Select(id => id.MapEntity() with { Group = group });
		group.MediaIds.AddRange(toAdd);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private static Expression<Func<MediaIdEntry, bool>> MatchesId(IMediaId mediaId)
	{
		var source = mediaId.Source;
		var id = mediaId.ToString()!;
		return entry => entry.Source == source && entry.ExternalId == id;
	}
}
