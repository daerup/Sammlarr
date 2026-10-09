using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Announcing;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Services;
using FeedsPage = UI.Features.Feeds.Pages.Feeds;

namespace UI.Tests.Feeds;

public class FeedsPageTests : BunitContext
{
	private static readonly FeedId Comedy = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	[Test]
	public async Task RunFeeds_FromThePageHeader_OpensTheDialogWithItsOwnPicks()
	{
		RegisterServices([Overview()]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll(".feed-card").Count == 1);

		await Buttons(cut, "Run feeds").Single().ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".run-title").Count == 1);
		await Assert.That(cut.FindAll(".run-from")).IsEmpty();
	}

	[Test]
	public async Task RunFeeds_WhileBulkEditing_IsHidden()
	{
		RegisterServices([Overview()]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.FindAll(".feed-card").Count == 1);

		await Buttons(cut, "Bulk edit").Single().ClickAsync(new());

		await Assert.That(Buttons(cut, "Run feeds")).IsEmpty();
	}

	[Test]
	public async Task RunFeeds_WithoutAnyFeed_IsNotOffered()
	{
		RegisterServices([]);
		var cut = RenderPage();
		cut.WaitForState(() => cut.Markup.Contains("No feeds yet."));

		await Assert.That(Buttons(cut, "Run feeds")).IsEmpty();
	}

	private void RegisterServices(IReadOnlyList<FeedOverview> overviews)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		AddAuthorization().SetAuthorized("ana");
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());

		var feeds = IFeedService.Mock();
		feeds.GetAsync(Any()).Returns(new FeedSnapshot(overviews));
		feeds.GetRunsAsync(Any(), Any()).Returns([]);
		Services.AddSingleton(feeds.Object);

		var queue = IJobQueueView.Mock();
		queue.GetAsync(Any()).Returns(new QueueSnapshot([], 0));
		Services.AddSingleton(queue.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(media.Object);

		Services.AddSingleton(IJobQueue.Mock().Object);
		Services.AddSingleton(IFeedRunPlanner.Mock().Object);
		Services.AddSingleton(IWantlistService.Mock().Object);
		Services.AddSingleton(IBlocklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddSingleton<IEventPublisher>(new TestEventPublisher());
		Services.AddScoped<Notifier>();
		Services.AddScoped<ProfileDropdownService>();
		Services.AddScoped<AccountConnectionPrompt>();
		Services.AddProviderBranding();
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private IRenderedComponent<IComponent> RenderPage() =>
		Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedsPage>(1);
			builder.CloseComponent();
		});

	private static List<IElement> Buttons(IRenderedComponent<IComponent> cut, string label) =>
		[.. cut.FindAll("button").Where(button => button.TextContent.Contains(label))];

	private static FeedOverview Overview() =>
		new(
			new Feed
			{
				Name = "Comedy",
				Id = Comedy,
				Enabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "hdlists/comedy",
				CreatedAt = DateTimeOffset.UtcNow,
				Schedule = new FeedRunTrigger(new Manual()),
			},
			new None(),
			new FeedRunHistory
			{
				TotalRuns = 0,
				FirstRunAt = new None(),
				TotalAdded = 0,
				TotalRemoved = 0,
			},
			0
		);
}
