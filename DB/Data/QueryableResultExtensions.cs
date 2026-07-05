using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DB.Data;

internal static class QueryableResultExtensions
{
	extension<T>(IQueryable<T> query)
		where T : class
	{
		public Task<Result<T>> SingleResultAsync(Expression<Func<T, bool>> predicate, string missingReason, CancellationToken cancellationToken = default) =>
			Result.Of(query.SingleOrDefaultAsync(predicate, cancellationToken), missingReason);
	}
}
