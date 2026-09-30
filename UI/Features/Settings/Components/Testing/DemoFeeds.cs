using Domain.Feed;
using Domain.Feed.Run;
using Domain.Jobs;
using Domain.Providers;
using UI.Features.Feeds.Components;
using UI.Features.Feeds.Pages;
using UI.Shared.Media;

namespace UI.Features.Settings.Components.Testing;

internal static class DemoFeeds
{
	public static readonly DateTimeOffset Now = DateTimeOffset.Now;

	public static readonly FeedRunTrigger Daily = new Scheduled("0 0 9 * * *");

	public static readonly Feed MirrorFeed = Create(
		"Latest digital releases",
		FeedProvider.Mdblist,
		"snoak/latest-movies-digital-release",
		new Mirror(3),
		Daily,
		enabled: true
	);

	public static readonly Feed TrendingFeed = Create("Trending this week", FeedProvider.Tmdb, "8321", new Append(), new Manual(), enabled: false);

	public static readonly FeedRun MirrorRun = Run(
		MirrorFeed,
		Conclusiveness.Conclusive,
		new FeedRunCounts
		{
			Total = 48,
			Added = 1,
			Kept = 45,
			Bypassed = 0,
			Filtered = 1,
			Skipped = 0,
			Unresolved = 0,
			Removed = 1,
		},
		[
			new FeedRunItem { MediaId = DemoMedia.Movie.Id, State = FeedRunItemState.Added },
			new FeedRunItem { MediaId = DemoMedia.Show.Id, State = FeedRunItemState.Removed },
		]
	);

	public static readonly FeedRun FailedRun = Run(TrendingFeed, Conclusiveness.NoItems, FeedRunCounts.None, []) with
	{
		SourceFailure = "The list could not be found (HTTP 404).",
	};

	public static readonly FeedRun GuardedRun = Run(MirrorFeed, Conclusiveness.NoMatches, FeedRunCounts.None with { Total = 20, Unresolved = 20 }, []);

	public static readonly Feed DocumentariesFeed = Create(
		"hdlists/latest-hd-documentary-movies-1980-to-today",
		FeedProvider.Mdblist,
		"hdlists/latest-hd-documentary-movies-1980-to-today",
		new Append(),
		new Scheduled("0 26 * * * *"),
		enabled: true
	);

	public static readonly FeedRun QuietRun = Run(DocumentariesFeed, Conclusiveness.Conclusive, FeedRunCounts.None with { Total = 402, Kept = 402 }, []);

	public static readonly IReadOnlyList<FeedRow> Rows =
	[
		new(
			DocumentariesFeed,
			new FeedOverview(
				QuietRun,
				new FeedRunHistory
				{
					TotalRuns = 15,
					FirstRunAt = Now.AddDays(-12),
					TotalAdded = 13913,
					TotalRemoved = 0,
				}
			),
			402,
			null,
			Now.AddMinutes(34)
		),
		new(
			MirrorFeed,
			new FeedOverview(
				MirrorRun,
				new FeedRunHistory
				{
					TotalRuns = 30,
					FirstRunAt = Now.AddDays(-30),
					TotalAdded = 180,
					TotalRemoved = 40,
				}
			),
			12,
			JobState.Running,
			Now.AddMinutes(94)
		),
		new(
			TrendingFeed,
			new FeedOverview(
				FailedRun,
				new FeedRunHistory
				{
					TotalRuns = 2,
					FirstRunAt = Now.AddDays(-3),
					TotalAdded = 9,
					TotalRemoved = 0,
				}
			),
			0,
			null,
			null
		),
	];

	public static readonly IReadOnlyList<RunCandidate> Candidates = RunPlan.Candidates(
		[.. Rows.Select(row => row.Feed)],
		Rows.Select(row => row.Overview).OfType<FeedOverview>().ToDictionary(overview => overview.LastRun.FeedId),
		Now,
		TimeZoneInfo.Local
	);

	private static Feed Create(string name, FeedProvider provider, string identifier, FeedStrategy strategy, FeedRunTrigger schedule, bool enabled) =>
		new()
		{
			Id = FeedId.From(Guid.NewGuid()),
			Name = name,
			Enabled = enabled,
			Provider = provider,
			Strategy = strategy,
			Identifier = identifier,
			CreatedAt = Now.AddDays(-30),
			Schedule = schedule,
		};

	private static FeedRun Run(Feed feed, Conclusiveness conclusiveness, FeedRunCounts counts, IReadOnlyList<FeedRunItem> items) =>
		new()
		{
			Id = Guid.NewGuid(),
			FeedId = feed.Id,
			StartedAt = Now.AddHours(-22),
			Duration = TimeSpan.FromSeconds(14),
			Strategy = feed.Strategy,
			Trigger = feed.Schedule,
			Conclusiveness = conclusiveness,
			Counts = counts,
			Items = items,
		};
}
