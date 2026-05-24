using System.Data;
using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal class MediaRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IMediaRepository
{
	public async Task<MediaIdentity?> FindAsync(IEnumerable<IExternalMediaId> externals, CancellationToken cancellationToken = default)
	{
		var materialized = externals.ToList();
		if (materialized.Count is 0)
		{
			return null;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var matched = await ctx.MediaMatchingAny(materialized).Include(m => m.ExternalIds).ToListAsync(cancellationToken);
		return matched.Count switch
		{
			0 => null,
			1 => ToIdentity(matched[0]),
			_ => throw new MediaIdentityConflictException([.. matched.Select(m => MediaId.From(m.Id))], materialized),
		};
	}

	public async Task<MediaIdentity?> FindAsync(MediaId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Media.Where(m => m.Id == id.Value).Include(m => m.ExternalIds).SingleOrDefaultAsync(cancellationToken);
		return entity is null ? null : ToIdentity(entity);
	}

	public async Task<MediaIdentity> UpsertAsync(IEnumerable<IExternalMediaId> externals, CancellationToken cancellationToken = default)
	{
		var deduped = externals.Distinct().ToList();
		deduped.EnsureSingleMediaType();

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		var matched = await ctx
			.ExternalMediaIds.Include(externalId => externalId.Media)
				.ThenInclude(media => media.ExternalIds)
			.WhereMatchingAny(deduped)
			.Select(externalId => externalId.Media)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (matched.Count > 1)
		{
			throw new MediaIdentityConflictException([.. matched.Select(m => MediaId.From(m.Id))], deduped);
		}

		if (matched.Count == 0)
		{
			var newMedia = new MediaEntity();
			newMedia.ExternalIds = deduped.ToExternalIdEntities(newMedia);
			await ctx.Media.AddAsync(newMedia, cancellationToken);
			await ctx.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return ToIdentity(newMedia);
		}

		var existing = matched[0];
		var current = existing.ExternalIds.Select(externalId => externalId.ToExternalMediaId()).ToHashSet();
		var missing = deduped.Where(id => !current.Contains(id)).ToList();
		missing.ForEach(id => existing.ExternalIds.Add(id.ToExternalIdEntity(existing)));
		if (missing.Count > 0)
		{
			await ctx.SaveChangesAsync(cancellationToken);
		}

		await transaction.CommitAsync(cancellationToken);
		return ToIdentity(existing);
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

	private static MediaIdentity ToIdentity(MediaEntity entity) => new(MediaId.From(entity.Id), entity.ExternalIds.Select(e => e.ToExternalMediaId()).ToList());
}
