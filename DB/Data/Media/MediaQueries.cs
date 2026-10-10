using System.Linq.Expressions;
using System.Reflection;
using Domain.Media;

namespace DB.Data.Media;

internal static class MediaQueries
{
	extension(IQueryable<ExternalMediaIdRow> source)
	{
		public IQueryable<ExternalMediaIdRow> WhereMatchingAny(IEnumerable<IExternalMediaId> mediaIds) => source.Where(BuildAnyMatchPredicate(mediaIds));
	}

	private static readonly MethodInfo ContainsMethod = typeof(Enumerable)
		.GetMethods()
		.First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
		.MakeGenericMethod(typeof(string));

	private static Expression<Func<ExternalMediaIdRow, bool>> BuildAnyMatchPredicate(IEnumerable<IExternalMediaId> mediaIds)
	{
		var groups = mediaIds
			.GroupBy(id => (id.Source, id.Type))
			.Select(g => (g.Key.Source, g.Key.Type, Ids: g.Select(id => id.Value).Distinct().ToArray()))
			.ToList();

		if (groups.Count is 0)
		{
			return _ => false;
		}

		var parameter = Expression.Parameter(typeof(ExternalMediaIdRow), "externalId");
		var source = Expression.Property(parameter, nameof(ExternalMediaIdRow.Source));
		var type = Expression.Property(parameter, nameof(ExternalMediaIdRow.MediaType));
		var id = Expression.Property(parameter, nameof(ExternalMediaIdRow.Id));

		Expression? body = null;
		foreach (var (idSource, idType, idValues) in groups)
		{
			var clause = Expression.AndAlso(
				Expression.AndAlso(Expression.Equal(source, Expression.Constant(idSource)), Expression.Equal(type, Expression.Constant(idType))),
				Expression.Call(null, ContainsMethod, Expression.Constant(idValues, typeof(IEnumerable<string>)), id)
			);
			body = body is null ? clause : Expression.OrElse(body, clause);
		}

		return Expression.Lambda<Func<ExternalMediaIdRow, bool>>(body!, parameter);
	}
}
