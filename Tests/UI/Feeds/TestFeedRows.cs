using Domain.Feed;
using Domain.Jobs;
using Domain.Providers;
using UI.Features.Feeds.Pages;

namespace UI.Tests.Feeds;

internal static class TestFeedRows
{
	internal static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 43, 0, TimeSpan.Zero);

	internal static readonly FeedRow Comedy = Row("hdlists/comedy-movies-2001-2020", FeedProvider.Mdblist, mirror: true, next: Now.AddMinutes(43), owned: 1);
	internal static readonly FeedRow Documentaries = Row("hdlists/latest-hd-documentary-movies-1980-to-today", FeedProvider.Mdblist, job: JobState.Running);
	internal static readonly FeedRow WeeklyPicks = Row("Weekly picks", FeedProvider.Tmdb, identifier: "8305012", enabled: false);
	internal static readonly FeedRow Trending = Row("snoak/trending-movies", FeedProvider.Mdblist, next: Now.AddHours(8), job: JobState.Queued, owned: 25);

	internal static readonly FeedRow[] All = [Comedy, Documentaries, WeeklyPicks, Trending];

	private static FeedRow Row(
		string name,
		FeedProvider provider,
		string? identifier = null,
		bool enabled = true,
		bool mirror = false,
		DateTimeOffset? next = null,
		JobState? job = null,
		int owned = 0
	) =>
		new(
			new Feed
			{
				Name = name,
				Id = FeedId.From(Guid.NewGuid()),
				Enabled = enabled,
				Provider = provider,
				Strategy = mirror ? new FeedStrategy(new Mirror()) : new FeedStrategy(new Append()),
				Identifier = identifier ?? name,
				CreatedAt = Now,
				Schedule = next is null ? new FeedRunTrigger(new Manual()) : new FeedRunTrigger(new Scheduled("0 26 * * * *")),
			},
			null,
			owned,
			job,
			next
		);
}
