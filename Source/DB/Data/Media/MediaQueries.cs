using System.Linq.Expressions;
using Contracts.Media;

namespace DB.Data.Media;

internal static class MediaQueries
{
	extension(IQueryable<ExternalMediaIdEntity> source)
	{
		public IQueryable<ExternalMediaIdEntity> WhereMatching(IMediaId mediaId)
		{
			var src = mediaId.Source;
			var type = mediaId.Type;
			var value = mediaId.Value;
			return source.Where(e => e.Source == src && e.MediaType == type && e.Id == value);
		}

		public IQueryable<ExternalMediaIdEntity> WhereMatchingAny(IEnumerable<IMediaId> mediaIds) => source.Where(BuildAnyMatchPredicate(mediaIds));

		public IQueryable<ExternalMediaIdEntity> WhereSource(MediaProvider provider) => source.Where(e => e.Source == provider);
	}

	extension(SammlarrDbContext ctx)
	{
		public IQueryable<MediaEntity> MediaMatching(IMediaId mediaId) => ctx.ExternalMediaIds.WhereMatching(mediaId).Select(e => e.Media);

		public IQueryable<MediaEntity> MediaMatchingAny(IEnumerable<IMediaId> mediaIds) =>
			ctx.ExternalMediaIds.WhereMatchingAny(mediaIds).Select(e => e.Media).Distinct();
	}

	private static Expression<Func<ExternalMediaIdEntity, bool>> BuildAnyMatchPredicate(IEnumerable<IMediaId> mediaIds)
	{
		var distinct = mediaIds.Select(id => (id.Source, id.Type, id.Value)).Distinct().ToList();
		if (distinct.Count is 0)
		{
			return _ => false;
		}

		var parameter = Expression.Parameter(typeof(ExternalMediaIdEntity), "externalId");
		var source = Expression.Property(parameter, nameof(ExternalMediaIdEntity.Source));
		var type = Expression.Property(parameter, nameof(ExternalMediaIdEntity.MediaType));
		var id = Expression.Property(parameter, nameof(ExternalMediaIdEntity.Id));

		Expression? body = null;
		foreach (var (idSource, idType, idValue) in distinct)
		{
			var triplet = Expression.AndAlso(
				Expression.AndAlso(Expression.Equal(source, Expression.Constant(idSource)), Expression.Equal(type, Expression.Constant(idType))),
				Expression.Equal(id, Expression.Constant(idValue))
			);
			body = body is null ? triplet : Expression.OrElse(body, triplet);
		}

		return Expression.Lambda<Func<ExternalMediaIdEntity, bool>>(body!, parameter);
	}
}
