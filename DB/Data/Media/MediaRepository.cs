using System.Data;
using Contracts.Media;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal sealed class MediaRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, MediaDetailsFactory detailsFactory, MediaWriteLock writeLock)
	: IMediaRepository
{
	public async Task<IReadOnlyList<MediaIdentity?>> FindIdentitiesAsync(
		IReadOnlyList<IReadOnlyList<IExternalMediaId>> candidateGroups,
		CancellationToken cancellationToken = default
	)
	{
		if (candidateGroups.Count == 0)
		{
			return [];
		}

		var union = candidateGroups.SelectMany(group => group).Distinct().ToList();
		if (union.Count == 0)
		{
			return new MediaIdentity?[candidateGroups.Count];
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var matched = await ctx.MediaMatchingAny(union).AsNoTracking().Include(m => m.ExternalIds).ToListAsync(cancellationToken);

		var identityByExternal = new Dictionary<IExternalMediaId, MediaIdentity>();
		foreach (var entity in matched)
		{
			var identity = ToIdentity(entity);
			foreach (var external in identity.Externals)
			{
				identityByExternal[external] = identity;
			}
		}

		var results = new MediaIdentity?[candidateGroups.Count];
		for (var i = 0; i < candidateGroups.Count; i++)
		{
			var group = candidateGroups[i];
			var matches = new HashSet<MediaIdentity>();
			foreach (var candidate in group)
			{
				if (identityByExternal.TryGetValue(candidate, out var identity))
				{
					matches.Add(identity);
				}
			}
			results[i] = matches.Count switch
			{
				0 => null,
				1 => matches.First(),
				_ => throw new MediaIdentityConflictException([.. matches.Select(m => m.Id)], [.. group]),
			};
		}
		return results;
	}

	public async Task<IReadOnlyList<MediaIdentity>> UpsertIdentitiesAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default)
	{
		if (identities.Count == 0)
		{
			return [];
		}

		foreach (var identity in identities)
		{
			identity.Externals.EnsureSingleMediaType();
		}

		var rawIds = identities.Select(i => i.Id.Value).ToList();
		var allExternals = identities.SelectMany(i => i.Externals).Distinct().ToList();
		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		var byId = await ctx.Media.Include(m => m.ExternalIds).Where(m => rawIds.Contains(m.Id)).ToListAsync(cancellationToken);
		var byExternal = await ctx.MediaMatchingAny(allExternals).Include(m => m.ExternalIds).ToListAsync(cancellationToken);
		var entities = byId.Concat(byExternal).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
		var ownerByExternal = entities
			.Values.SelectMany(e => e.ExternalIds.Select(x => (Key: x.ToExternalMediaId(), Owner: e)))
			.ToDictionary(t => t.Key, t => t.Owner);

		var result = identities.Select(identity => UpsertOne(ctx, identity, entities, ownerByExternal)).ToList();

		await ctx.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return result;
	}

	public async Task<IReadOnlyList<StoredMedia>> GetAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Count == 0)
		{
			return [];
		}
		var rawIds = ids.Select(id => id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await QueryStoredAsync(ctx.Media.Where(m => rawIds.Contains(m.Id)), cancellationToken);
	}

	public async Task<IReadOnlyList<StoredMedia>> GetByExternalsAsync(IReadOnlyList<IExternalMediaId> candidates, CancellationToken cancellationToken = default)
	{
		if (candidates.Count == 0)
		{
			return [];
		}
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaIds = ctx.ExternalMediaIds.WhereMatchingAny(candidates).Select(e => e.Media.Id).Distinct();
		return await QueryStoredAsync(ctx.Media.Where(m => mediaIds.Contains(m.Id)), cancellationToken);
	}

	private async Task<IReadOnlyList<StoredMedia>> QueryStoredAsync(IQueryable<MediaRow> media, CancellationToken cancellationToken)
	{
		var rows = await media
			.AsNoTracking()
			.Include(m => m.ExternalIds)
			.Include(m => m.Details!)
				.ThenInclude(details => details.Ratings)
			.AsSplitQuery()
			.ToListAsync(cancellationToken);

		return [.. rows.Select(row => new StoredMedia(ToIdentity(row), Optional.Of(row.Details).Map(detailsFactory.ToCached)))];
	}

	public async Task UpsertDetailsAsync(IReadOnlyDictionary<MediaId, CachedDetails> details, CancellationToken cancellationToken = default)
	{
		if (details.Count == 0)
		{
			return;
		}

		using var _ = await writeLock.AcquireAsync(cancellationToken);
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rawIds = details.Keys.Select(id => id.Value).ToList();
		var existing = await ctx.MediaDetails.Include(m => m.Ratings).Where(m => rawIds.Contains(m.MediaId)).ToDictionaryAsync(m => m.MediaId, cancellationToken);

		foreach (var (id, cached) in details)
		{
			if (existing.TryGetValue(id.Value, out var entity))
			{
				detailsFactory.Update(entity, cached);
			}
			else
			{
				ctx.MediaDetails.Add(detailsFactory.BuildFrom(cached, id.Value));
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task RemoveDetailsAsync(IReadOnlyList<MediaId> ids, CancellationToken cancellationToken = default)
	{
		if (ids.Count == 0)
		{
			return;
		}

		var rawIds = ids.Select(id => id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaDetails.Where(m => rawIds.Contains(m.MediaId)).ExecuteDeleteAsync(cancellationToken);
	}

	public async Task<IReadOnlyDictionary<MediaId, IExternalMediaId>> GetExternalsAsync(
		IEnumerable<MediaId> ids,
		MediaProvider source,
		CancellationToken cancellationToken = default
	)
	{
		var rawIds = ids.Distinct().Select(id => id.Value).ToList();
		if (rawIds.Count is 0)
		{
			return new Dictionary<MediaId, IExternalMediaId>();
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx
			.ExternalMediaIds.AsNoTracking()
			.Where(e => e.Source == source && rawIds.Contains(e.Media.Id))
			.Select(e => new { MediaId = e.Media.Id, External = e })
			.ToListAsync(cancellationToken);
		return rows.ToDictionary(r => MediaId.From(r.MediaId), r => r.External.ToExternalMediaId());
	}

	private static MediaIdentity UpsertOne(
		SammlarrDbContext ctx,
		MediaIdentity identity,
		Dictionary<Guid, MediaRow> entities,
		Dictionary<IExternalMediaId, MediaRow> ownerByExternal
	)
	{
		var owners = identity.Externals.Select(ownerByExternal.GetValueOrDefault).Where(o => o is not null).Select(o => o!).DistinctBy(o => o.Id).ToList();
		if (owners.Count > 1)
		{
			throw new MediaIdentityConflictException([.. owners.Select(o => MediaId.From(o.Id))], [.. identity.Externals]);
		}

		var entity = owners.FirstOrDefault() ?? entities.GetValueOrDefault(identity.Id.Value);
		if (entity is null)
		{
			entity = new MediaRow { Id = identity.Id.Value };
			ctx.Media.Add(entity);
			entities[entity.Id] = entity;
		}

		foreach (var external in identity.Externals.Where(e => !ownerByExternal.ContainsKey(e)))
		{
			entity.ExternalIds.Add(external.ToExternalIdEntity(entity));
			ownerByExternal[external] = entity;
		}

		return ToIdentity(entity);
	}

	private static MediaIdentity ToIdentity(MediaRow entity) => new(MediaId.From(entity.Id), [.. entity.ExternalIds.Select(e => e.ToExternalMediaId())]);
}
