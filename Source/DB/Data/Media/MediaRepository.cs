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

		var deduped = identities
			.Select(i =>
			{
				var externals = i.Externals.Distinct().ToList();
				externals.EnsureSingleMediaType();
				return i with { Externals = externals };
			})
			.ToList();

		var rawIds = deduped.Select(i => i.Id.Value).ToList();
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		var existing = await ctx.Media.Include(m => m.ExternalIds).Where(m => rawIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken);

		var result = deduped.Select(identity => UpsertOne(ctx, identity, existing)).ToList();

		await ctx.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return result;
	}

	private static MediaIdentity UpsertOne(SammlarrDbContext ctx, MediaIdentity identity, Dictionary<Guid, MediaEntity> existing)
	{
		if (existing.TryGetValue(identity.Id.Value, out var entity))
		{
			var known = entity.ExternalIds.Select(e => e.ToExternalMediaId()).ToHashSet();
			foreach (var external in identity.Externals.Where(e => !known.Contains(e)))
			{
				entity.ExternalIds.Add(external.ToExternalIdEntity(entity));
			}
			return ToIdentity(entity);
		}

		entity = new MediaEntity { Id = identity.Id.Value };
		entity.ExternalIds = identity.Externals.ToExternalIdEntities(entity);
		ctx.Media.Add(entity);
		existing[identity.Id.Value] = entity;
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

	private static MediaIdentity ToIdentity(MediaEntity entity) => new(MediaId.From(entity.Id), entity.ExternalIds.Select(e => e.ToExternalMediaId()).ToList());
}
