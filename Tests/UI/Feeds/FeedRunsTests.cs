using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blacklist;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Runs;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedRunsTests : BunitContext
{
	private static readonly FeedId Feed = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	private static readonly IReadOnlyList<FeedRun> Runs = [.. Enumerable.Range(0, 30).Select(hoursAgo => Run(DateTimeOffset.UtcNow.AddHours(-hoursAgo)))];

	[Test]
	public async Task Render_WhenTheLinkedRunIsBehindLoadOlder_ListsItAndScrollsToIt()
	{
		var linked = Runs[27];
		RegisterServices(linked);
		var scroll = JSInterop.SetupVoid("sammlarrScroll.intoView", "run-selected");
		scroll.SetVoidResult();

		var cut = Render<FeedRuns>(parameters =>
			parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, UrlGuid.Encode(linked.Id))
		);
		cut.WaitForState(() => scroll.Invocations.Count == 1);

		await Assert.That(cut.FindAll(".runs-item")).Count().IsEqualTo(28);
		await Assert.That(cut.FindAll("#run-selected")).HasSingleItem();
	}

	private void RegisterServices(FeedRun linked)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		var feeds = IFeedService.Mock();
		feeds
			.GetFeedByIdAsync(Any(), Any())
			.Returns(
				new Domain.Feed.Feed
				{
					Name = "Comedy",
					Id = Feed,
					Enabled = true,
					Provider = FeedProvider.Mdblist,
					Strategy = new FeedStrategy(new Append()),
					Identifier = "hdlists/comedy",
					CreatedAt = Runs[^1].StartedAt,
					Schedule = new FeedRunTrigger(new Manual()),
				}
			);
		feeds
			.GetHistoryAsync(Any(), Any())
			.Returns(
				new FeedRunHistory
				{
					TotalRuns = Runs.Count,
					FirstRunAt = Runs[^1].StartedAt,
					TotalAdded = 0,
					TotalRemoved = 0,
				}
			);
		feeds.GetRunsAsync(Any(), Any(), Any()).Returns((_, take, _) => [.. Runs.Take(take)]);
		feeds.GetRunAsync(Any(), Any()).Returns(Some.Of(linked));
		feeds.CountRunsSinceAsync(Any(), Any(), Any()).Returns((_, since, _) => Runs.Count(run => run.StartedAt >= since));
		Services.AddSingleton(feeds.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(AsyncEnumerable.Empty<Result<IdentifiedMedia>>());
		Services.AddSingleton(media.Object);

		var watchlist = IWatchlistRepository.Mock();
		watchlist.GetOwnedCountsAsync(Any()).Returns(new Dictionary<FeedId, int>());
		Services.AddSingleton(watchlist.Object);

		Services.AddSingleton(IWatchlistService.Mock().Object);
		Services.AddSingleton(IBlacklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigRepository.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private static FeedRun Run(DateTimeOffset startedAt) =>
		new()
		{
			Id = Guid.NewGuid(),
			FeedId = Feed,
			StartedAt = startedAt,
			Duration = TimeSpan.FromSeconds(2),
			Strategy = new FeedStrategy(new Append()),
			Trigger = new FeedRunTrigger(new Manual()),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = FeedRunCounts.None,
			Items = [],
		};
}
