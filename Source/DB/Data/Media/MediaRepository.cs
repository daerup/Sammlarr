using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Media;

internal class MediaRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IMediaRepository
{
	private const string MixedMediaTypesMessage = "All IDs must be of the same media type.";

	public async Task<List<IMediaId>> GetByIdsAsync(IEnumerable<IMediaId> mediaIds, CancellationToken cancellationToken = default)
	{
		var ids = mediaIds.Distinct().ToList();
		if (ids.Count is 0)
		{
			return [];
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var matched = await ctx
			.ExternalMediaIds.WhereMatchingAny(ids)
			.SelectMany(externalId => externalId.Media.ExternalIds)
			.Distinct()
			.ToListAsync(cancellationToken);
		return matched.Select(externalId => externalId.ToMediaId()).ToList();
	}

	public async Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.Distinct().ToList();
		if (idList.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(idList);

		var media = new MediaEntity();
		media.ExternalIds = idList.ToExternalIds(media);

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Media.AddAsync(media, cancellationToken);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task UpsertAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var idList = ids.Distinct().ToList();
		if (idList.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(idList);

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var existingMedia = await ctx
			.ExternalMediaIds.Include(externalId => externalId.Media)
				.ThenInclude(media => media.ExternalIds)
			.WhereMatchingAny(idList)
			.Select(externalId => externalId.Media)
			.Distinct()
			.SingleOrDefaultAsync(cancellationToken);

		if (existingMedia is null)
		{
			var newMedia = new MediaEntity();
			newMedia.ExternalIds = idList.ToExternalIds(newMedia);
			await ctx.Media.AddAsync(newMedia, cancellationToken);
		}
		else
		{
			var existing = existingMedia.ExternalIds.Select(externalId => externalId.ToMediaId()).ToHashSet();
			var missing = idList.Where(id => !existing.Contains(id)).ToList();
			ValidateSingleMediaType(existing.Concat(missing));
			foreach (var mediaId in missing)
			{
				existingMedia.ExternalIds.Add(mediaId.ToExternalId(existingMedia));
			}
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static void ValidateSingleMediaType(IEnumerable<IMediaId> mediaIds)
	{
		if (mediaIds.Select(id => id.Type).Distinct().Take(2).Count() > 1)
		{
			throw new ArgumentException(MixedMediaTypesMessage);
		}
	}
}
