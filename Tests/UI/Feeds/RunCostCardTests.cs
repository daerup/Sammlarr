using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class RunCostCardTests : BunitContext
{
	private static readonly FeedRun Run = TestFeeds.Run(TestFeeds.Feed(Guid.NewGuid()), Guid.NewGuid()) with
	{
		Duration = TimeSpan.FromSeconds(21.4),
		Counts = FeedRunCounts.None with { Total = 300, UnmatchedNotFound = 2, UnmatchedCouldNotLookUp = 1 },
		Calls =
		[
			new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 290),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.Answered, 520),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.AnsweredAfter429, 37),
			new FeedRunCalls(RemoteService.Plex, UsageOutcome.GaveUpOn429, 4),
		],
	};

	private readonly TestCurrentUser _user = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton<ICurrentUser>(_user);
	}

	[Test]
	public async Task Render_ARunsCalls_ListsEachServiceBusiestFirstWithItsRateLimits()
	{
		var cut = Render<RunCostCard>(parameters => parameters.Add(card => card.Run, Run));

		var services = cut.FindAll("[data-testid=run-cost-service]");
		await Assert.That(services.Select(row => row.GetAttribute("data-service")!)).IsEquivalentTo(["Plex", "Tmdb"], CollectionOrdering.Matching);
		await Assert.That(services[0].TextContent).Contains("41 rate-limited, 37 answered");
		await Assert
			.That(cut.Find("[data-testid=run-cost-summary]").TextContent)
			.Contains("851 calls to 2 services for 300 titles (2.84 per title) in 21.4 s")
			.And.Contains("41 rate-limited, 37 of them still answered");
	}

	[Test]
	public async Task Render_TheUnmatchedTitles_SplitsNotFoundFromCouldNotLookUp()
	{
		var cut = Render<RunCostCard>(parameters => parameters.Add(card => card.Run, Run));

		await Assert
			.That(cut.Find("[data-testid=run-cost-unmatched]").TextContent)
			.Contains("3 of 300 titles: 2 not found by any service, 1 could not be looked up (tried again next run)");
	}

	[Test]
	public async Task Render_ForAMember_LeavesOutTheLinkToUsage()
	{
		_user.Role = UserRole.Member;

		var cut = Render<RunCostCard>(parameters => parameters.Add(card => card.Run, Run));

		await Assert.That(cut.FindAll("a[href='/settings?section=usage']")).IsEmpty();
	}

	[Test]
	public async Task Render_ARunWithoutCalls_ShowsNothing()
	{
		var cut = Render<RunCostCard>(parameters => parameters.Add(card => card.Run, Run with { Calls = [] }));

		await Assert.That(cut.FindAll("[data-testid=run-cost]")).IsEmpty();
	}
}
