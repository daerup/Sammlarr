using BlazorBlueprint.Components;
using Bunit;
using Contracts;
using Contracts.Announcing;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Providers;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Jobs;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Components;
using UI.Shared.Layout;
using UI.Tests.Feeds;

namespace UI.Tests.Layout;

public class FeedsNavDotTests : BunitContext
{
	private static readonly Feed Documentaries = TestFeedRows.Documentaries.Overview.Feed;

	private readonly Mock<IJobQueueView> _queue = IJobQueueView.Mock();

	private readonly TestEventPublisher _events = new();

	private readonly TestCurrentUser _user = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_queue.GetAsync(Any()).Returns(new QueueSnapshot([], 0));
		Services.AddSingleton(_queue.Object);
		Services.AddSingleton<IEventPublisher>(_events);
		Services.AddSingleton<ICurrentUser>(_user);
		var feeds = IFeedService.Mock();
		feeds.GetAsync(Any()).Returns(new FeedSnapshot([TestFeedRows.Documentaries.Overview]));
		Services.AddSingleton(feeds.Object);
		var layouts = IFeedRunLayout.Mock();
		layouts.GetAsync(Any<FeedId>(), Any()).Returns(RunLayout.Empty);
		Services.AddSingleton(layouts.Object);
		var traces = IFeedRunTraces.Mock();
		traces.ReadAsync(Any<FeedId>(), Any<TraceCursor>(), Any()).Returns(new NoTrace());
		Services.AddSingleton(traces.Object);
		var capacity = IRemoteCapacity.Mock();
		capacity.Of(Any<RemoteService>()).Returns(new RemoteLoad(new None(), 0));
		Services.AddSingleton(capacity.Object);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UtcNow);
		Services.AddSingleton(clock.Object);
		Services.AddScoped<FeedRunProgress>();
	}

	[Test]
	public async Task Render_WhileYourFeedRuns_ShowsTheDot()
	{
		var cut = Render<FeedsNavDot>();

		_events.Publish(Job(TestCurrentUser.DefaultId));

		cut.WaitForState(() => cut.FindAll("[data-testid=feeds-nav-dot]").Count == 1);
		await Assert.That(cut.Find("[data-testid=feeds-nav-dot]").GetAttribute("aria-label")).IsEqualTo("A feed is running");
	}

	[Test]
	public async Task Render_ForAnotherUsersRun_ShowsNothing()
	{
		var cut = Render<FeedsNavDot>();

		_events.Publish(Job(UserId.From(Guid.NewGuid())));
		await Task.Delay(300);

		await Assert.That(cut.FindAll("[data-testid=feeds-nav-dot]")).IsEmpty();
	}

	[Test]
	public async Task Render_OnTheFeedsPage_ShowsNothing()
	{
		Services.GetRequiredService<NavigationManager>().NavigateTo("/feeds");
		var cut = Render<FeedsNavDot>();

		_events.Publish(Job(TestCurrentUser.DefaultId));
		cut.WaitForState(() => Services.GetRequiredService<FeedRunProgress>().Live.IsSome());

		await Assert.That(cut.FindAll("[data-testid=feeds-nav-dot]")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenSignedOut_ShowsNothing()
	{
		_user.IsAuthenticated = false;
		var cut = Render<FeedsNavDot>();

		_events.Publish(Job(TestCurrentUser.DefaultId));
		await Task.Delay(300);

		await Assert.That(cut.FindAll("[data-testid=feeds-nav-dot]")).IsEmpty();
	}

	private static JobStateChangedEvent Job(UserId owner) => new(owner, JobKind.FeedRun, Documentaries.Id.Value.ToString(), JobState.Running);
}
