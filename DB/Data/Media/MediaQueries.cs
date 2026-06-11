using System.Linq.Expressions;
using System.Reflection;
using Contracts.Media;

namespace DB.Data.Media;

internal static class MediaQueries
{
	extension(IQueryable<ExternalMediaIdEntity> source)
	{
		public IQueryable<ExternalMediaIdEntity> WhereMatchingAny(IEnumerable<IExternalMediaId> mediaIds) => source.Where(BuildAnyMatchPredicate(mediaIds));
	}

	extension(SammlarrDbContext ctx)
	{
		public IQueryable<MediaEntity> MediaMatchingAny(IEnumerable<IExternalMediaId> mediaIds) =>
			ctx.ExternalMediaIds.WhereMatchingAny(mediaIds).Select(e => e.Media).Distinct();
	}

	private static readonly MethodInfo ContainsMethod = typeof(Enumerable)
		.GetMethods()
		.First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
		.MakeGenericMethod(typeof(string));

	private static Expression<Func<ExternalMediaIdEntity, bool>> BuildAnyMatchPredicate(IEnumerable<IExternalMediaId> mediaIds)
	{
		var groups = mediaIds
			.GroupBy(id => (id.Source, id.Type))
			.Select(g => (g.Key.Source, g.Key.Type, Ids: g.Select(id => id.Value).Distinct().ToArray()))
			.ToList();

		if (groups.Count is 0)
		{
			return _ => false;
		}

		var parameter = Expression.Parameter(typeof(ExternalMediaIdEntity), "externalId");
		var source = Expression.Property(parameter, nameof(ExternalMediaIdEntity.Source));
		var type = Expression.Property(parameter, nameof(ExternalMediaIdEntity.MediaType));
		var id = Expression.Property(parameter, nameof(ExternalMediaIdEntity.Id));

		Expression? body = null;
		foreach (var (idSource, idType, idValues) in groups)
		{
			var clause = Expression.AndAlso(
				Expression.AndAlso(Expression.Equal(source, Expression.Constant(idSource)), Expression.Equal(type, Expression.Constant(idType))),
				Expression.Call(null, ContainsMethod, Expression.Constant(idValues, typeof(IEnumerable<string>)), id)
			);
			body = body is null ? clause : Expression.OrElse(body, clause);
		}

		return Expression.Lambda<Func<ExternalMediaIdEntity, bool>>(body!, parameter);
	}
}
