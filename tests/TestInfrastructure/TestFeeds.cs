using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Wantlist;

namespace TestInfrastructure;

public static class TestFeeds
{
	private static readonly DateTimeOffset epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	public static Feed Feed(Guid id, string name = "Comedy", FeedProvider provider = FeedProvider.Mdblist)
	{
		return new()
		{
			Id = FeedId.From(id),
			Name = name,
			Enabled = true,
			Provider = provider,
			Strategy = new FeedStrategy(new Append()),
			Identifier = "test-list",
			CreatedAt = epoch,
			Schedule = new FeedRunTrigger(new Manual()),
		};
	}

	public static FeedRun Run(Feed feed, Guid id, Conclusiveness conclusiveness = Conclusiveness.Conclusive) =>
		new()
		{
			Id = RunId.From(id),
			FeedId = feed.Id,
			StartedAt = epoch,
			Duration = TimeSpan.Zero,
			Strategy = feed.Strategy,
			Trigger = feed.Schedule,
			Conclusiveness = conclusiveness,
			Counts = FeedRunCounts.None,
			Items = [],
		};

	public static FeedOrigin Origin(Guid feedId, Guid runId, string name = "Comedy", FeedProvider provider = FeedProvider.Mdblist)
	{
		var feed = Feed(feedId, name, provider);
		return new FeedOrigin(feed, Run(feed, runId));
	}
}
