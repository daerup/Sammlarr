using System.Data;
using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal class MediaRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IMediaRepository
{
	public async Task<IReadOnlyList<MediaIdentity?>> FindManyAsync(
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
		var matched = await ctx.MediaMatchingAny(union).Include(m => m.ExternalIds).ToListAsync(cancellationToken);

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

	public async Task<IReadOnlyList<MediaIdentity>> UpsertManyAsync(IReadOnlyList<MediaIdentity> identities, CancellationToken cancellationToken = default)
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

	private static MediaIdentity UpsertOne(
		SammlarrDbContext ctx,
		MediaIdentity identity,
		Dictionary<Guid, MediaEntity> entities,
		Dictionary<IExternalMediaId, MediaEntity> ownerByExternal
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
			entity = new MediaEntity { Id = identity.Id.Value };
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

	public IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<IExternalMediaId> candidates)
	{
		var ctx = ((MediaQuerySession)session).Context;
		return ctx.ExternalMediaIds.WhereMatchingAny(candidates).Select(e => e.Media.Id).Distinct();
	}

	public IQueryable<Guid> QueryMediaIds(IMediaQuerySession session, IEnumerable<MediaId> ids)
	{
		var ctx = ((MediaQuerySession)session).Context;
		var rawIds = ids.Select(id => id.Value).ToList();
		return ctx.Media.Where(m => rawIds.Contains(m.Id)).Select(m => m.Id);
	}

	private static MediaIdentity ToIdentity(MediaEntity entity) => new(MediaId.From(entity.Id), [.. entity.ExternalIds.Select(e => e.ToExternalMediaId())]);
}
