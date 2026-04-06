using System.Linq.Expressions;
using Contracts.Media;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

internal class MediaIdMappingMappingStore(SammlarrDbContext dbContext) : IMediaIdMappingStore
{
	public async Task<List<IMediaId>> GetByIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var s = await dbContext
			.MediaIds.Where(e => e.Group.MediaIds.AsQueryable().Any(MatchesId(mediaId)))
			.Select(e => e.MapValueObject())
			.ToListAsync(cancellationToken);

		if (s.Any())
		{
			return await dbContext
				.MediaIds.Where(e => e.Group.MediaIds.AsQueryable().Any(MatchesId(mediaId)))
				.Select(e => e.MapValueObject())
				.ToListAsync(cancellationToken);
		}
		return new List<IMediaId>();
	}

	public async Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.DistinctBy(id => (id.Source, id.ToString())).ToList();
		await dbContext.MediaIdGroups.AddAsync(new MediaIdGroup { MediaIds = [.. idList.Select(id => id.MapEntity())] }, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task CreateOrAppendAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.DistinctBy(id => (id.Source, id.ToString())).ToList();
		if (idList.Count is 0)
		{
			return;
		}

		var existingGroup = await dbContext
			.MediaIds.Include(e => e.Group)
				.ThenInclude(g => g.MediaIds)
			.Where(MatchesAnyId(idList))
			.Select(e => e.Group)
			.Distinct()
			.SingleOrDefaultAsync(cancellationToken);

		var group = existingGroup ?? new MediaIdGroup();
		var mediaIdEntries = idList.Select(id => id.MapEntity() with { Group = group });
		group.MediaIds = [.. group.MediaIds.UnionBy(mediaIdEntries, e => (e.Source, e.ExternalId))];

		if (existingGroup is null)
		{
			await dbContext.MediaIdGroups.AddAsync(group, cancellationToken);
		}

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
		await dbContext.MediaIds.AddRangeAsync(toAdd, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private static Expression<Func<MediaIdEntry, bool>> MatchesId(IMediaId mediaId)
	{
		var source = mediaId.Source;
		var id = mediaId.ToString()!;
		return entry => entry.Source == source && entry.ExternalId == id;
	}

	private static Expression<Func<MediaIdEntry, bool>> MatchesAnyId(IEnumerable<IMediaId> mediaIds)
	{
		var mediaIdList = mediaIds.ToList();
		if (mediaIdList.Count is 0)
			return _ => false;

		var parameter = Expression.Parameter(typeof(MediaIdEntry), "entry");
		var sourceProperty = Expression.Property(parameter, nameof(MediaIdEntry.Source));
		var externalIdProperty = Expression.Property(parameter, nameof(MediaIdEntry.ExternalId));

		Expression? body = null;
		foreach (var mediaId in mediaIdList)
		{
			var source = mediaId.Source;
			var id = mediaId.ToString()!;

			var andExpr = Expression.AndAlso(
				Expression.Equal(sourceProperty, Expression.Constant(source)),
				Expression.Equal(externalIdProperty, Expression.Constant(id))
			);

			body = body is null ? andExpr : Expression.OrElse(body, andExpr);
		}

		return Expression.Lambda<Func<MediaIdEntry, bool>>(body!, parameter);
	}
}
