using System.Linq.Expressions;
using Domain.Feed;

namespace DB.Data.Feed;

internal static class FeedProjection
{
	public static readonly Expression<Func<FeedRow, Domain.Feed.Feed>> Project = row => new Domain.Feed.Feed
	{
		Id = FeedId.From(row.Id),
		Name = row.Name,
		Enabled = row.IsEnabled,
		Provider = row.Provider,
		Strategy = row.Strategy,
		Identifier = row.Identifier,
		CreatedAt = row.CreatedAt,
		Filter = row.Filter,
		Sort = row.Sort,
		Limit = row.Limit,
		Schedule = row.Trigger,
	};

	private static readonly Func<FeedRow, Domain.Feed.Feed> map = Project.Compile();

	public static Domain.Feed.Feed ToDomain(this FeedRow row) => map(row);
}
