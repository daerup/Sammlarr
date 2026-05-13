using Contracts.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.MediaGroup;

internal class MediaIdGroupRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IMediaIdGroupRepository
{
	private const string MixedMediaTypesMessage = "All IDs must be of the same media type.";

	public async Task<List<IMediaId>> GetByIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.MediaIdEntries.Where(MediaIdentifierGroupEntryExpressions.MatchesId(mediaId))
			.Select(entry => entry.MediaIdGroupEntity.MediaIdEntries)
			.SelectMany(ids => ids)
			.Select(entry => entry.Identifier.ToMediaId())
			.ToListAsync(cancellationToken);
	}

	public async Task CreateAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var identifiers = ids.Select(MediaIdentifier.From).Distinct().ToList();
		if (identifiers.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(identifiers);

		var mediaIdGroup = new MediaIdentifierGroupEntity();
		mediaIdGroup.MediaIdEntries = identifiers.ToIdentifierEntries(mediaIdGroup);

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.MediaIdGroups.AddAsync(mediaIdGroup, cancellationToken);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task CreateOrAppendAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var identifiers = ids.Select(MediaIdentifier.From).Distinct().ToList();
		if (identifiers.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(identifiers);

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var existingGroup = await ctx
			.MediaIdEntries.Include(entry => entry.MediaIdGroupEntity)
				.ThenInclude(group => group.MediaIdEntries)
			.Where(MediaIdentifierGroupEntryExpressions.MatchesAnyIdentifier(identifiers))
			.Select(entry => entry.MediaIdGroupEntity)
			.Distinct()
			.SingleOrDefaultAsync(cancellationToken);

		var mediaIdGroup = existingGroup ?? new MediaIdentifierGroupEntity();
		var mergedIdentifiers = mediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier).Union(identifiers).ToList();
		mediaIdGroup.MediaIdEntries = mergedIdentifiers.ToIdentifierEntries(mediaIdGroup);

		ValidateSingleMediaType(mediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier));

		if (existingGroup is null)
		{
			await ctx.MediaIdGroups.AddAsync(mediaIdGroup, cancellationToken);
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task AppendAsync(IMediaId existingId, IEnumerable<IMediaId> newIds, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var mediaIdGroup = (
			await ctx
				.MediaIdEntries.Include(entry => entry.MediaIdGroupEntity)
					.ThenInclude(group => group.MediaIdEntries)
				.SingleAsync(MediaIdentifierGroupEntryExpressions.MatchesId(existingId), cancellationToken)
		).MediaIdGroupEntity;

		var storedIdentifiers = mediaIdGroup.MediaIdEntries.Select(x => x.Identifier).ToList();
		var appendCandidates = newIds.Select(MediaIdentifier.From).Except(storedIdentifiers).ToList();
		if (appendCandidates.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(appendCandidates.Concat(storedIdentifiers));

		var toAdd = appendCandidates.ToIdentifierEntries(mediaIdGroup);
		await ctx.MediaIdEntries.AddRangeAsync(toAdd, cancellationToken);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	private static void ValidateSingleMediaType(IEnumerable<MediaIdentifier> identifiers)
	{
		if (identifiers.Select(identifier => identifier.MediaType).Distinct().Take(2).Count() > 1)
		{
			throw new ArgumentException(MixedMediaTypesMessage);
		}
	}
}
