using System.Linq.Expressions;
using Contracts.Media;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

internal class MediaIdGroupStore(SammlarrDbContext dbContext) : IMediaIdGroupStore
{
	private const string MixedMediaTypesMessage = "All IDs must be of the same media type.";

	public async Task<List<IMediaId>> GetByIdAsync(IMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return await dbContext
			.MediaIdEntries.Where(MatchesId(mediaId))
			.Select(entry => entry.MediaIdGroup.MediaIdEntries)
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

		var mediaIdGroup = new MediaIdentifierGroup();
		mediaIdGroup.MediaIdEntries = identifiers.ToIdentifierEntries(mediaIdGroup);

		await dbContext.MediaIdGroups.AddAsync(mediaIdGroup, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task CreateOrAppendAsync(IEnumerable<IMediaId> ids, CancellationToken cancellationToken = default)
	{
		var identifiers = ids.Select(MediaIdentifier.From).Distinct().ToList();
		if (identifiers.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(identifiers);

		var existingGroup = await dbContext
			.MediaIdEntries.Include(entry => entry.MediaIdGroup)
				.ThenInclude(group => group.MediaIdEntries)
			.Where(MatchesAnyIdentifier(identifiers))
			.Select(entry => entry.MediaIdGroup)
			.Distinct()
			.SingleOrDefaultAsync(cancellationToken);

		var mediaIdGroup = existingGroup ?? new MediaIdentifierGroup();
		var mergedIdentifiers = mediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier).Union(identifiers).ToList();
		mediaIdGroup.MediaIdEntries = mergedIdentifiers.ToIdentifierEntries(mediaIdGroup);

		ValidateSingleMediaType(mediaIdGroup.MediaIdEntries.Select(entry => entry.Identifier));

		if (existingGroup is null)
		{
			await dbContext.MediaIdGroups.AddAsync(mediaIdGroup, cancellationToken);
		}

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task AppendAsync(IMediaId existingId, IEnumerable<IMediaId> newIds, CancellationToken cancellationToken = default)
	{
		var mediaIdGroup = (
			await dbContext
				.MediaIdEntries.Include(entry => entry.MediaIdGroup)
					.ThenInclude(group => group.MediaIdEntries)
				.SingleAsync(MatchesId(existingId), cancellationToken)
		).MediaIdGroup;

		var storedIdentifiers = mediaIdGroup.MediaIdEntries.Select(x => x.Identifier).ToList();
		var appendCandidates = newIds.Select(MediaIdentifier.From).Except(storedIdentifiers).ToList();
		if (appendCandidates.Count is 0)
		{
			return;
		}

		ValidateSingleMediaType(appendCandidates.Concat(storedIdentifiers));

		var toAdd = appendCandidates.ToIdentifierEntries(mediaIdGroup);
		await dbContext.MediaIdEntries.AddRangeAsync(toAdd, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private static void ValidateSingleMediaType(IEnumerable<MediaIdentifier> identifiers)
	{
		if (identifiers.Select(identifier => identifier.MediaType).Distinct().Take(2).Count() > 1)
		{
			throw new ArgumentException(MixedMediaTypesMessage);
		}
	}

	private static Expression<Func<MediaIdentifierEntry, bool>> MatchesId(IMediaId mediaId)
	{
		var identifier = MediaIdentifier.From(mediaId);
		return entry => entry.Identifier == identifier;
	}

	private static Expression<Func<MediaIdentifierEntry, bool>> MatchesAnyIdentifier(IEnumerable<MediaIdentifier> identifiers)
	{
		var identifierList = identifiers.ToList();
		if (identifierList.Count is 0)
		{
			return _ => false;
		}

		var parameter = Expression.Parameter(typeof(MediaIdentifierEntry), "entry");
		var identifierProperty = Expression.Property(parameter, nameof(MediaIdentifierEntry.Identifier));

		Expression? body = null;
		foreach (var identifier in identifierList)
		{
			var equalsExpr = Expression.Equal(identifierProperty, Expression.Constant(identifier));
			body = body is null ? equalsExpr : Expression.OrElse(body, equalsExpr);
		}

		return Expression.Lambda<Func<MediaIdentifierEntry, bool>>(body!, parameter);
	}
}
