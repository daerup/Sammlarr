using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Providers;
using Contracts.Wantlist;
using Core.Jobs;
using Core.Media;
using Domain.Blocklist;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Domain.Wantlist;
using Microsoft.Extensions.DependencyInjection;
using Observability.Usage;
using TestInfrastructure;

namespace Core.Tests.Media;

public class DetailsFillTests
{
	private static readonly FieldNeeds EveryFieldShown = new(MediaFields.None, MediaFields.Everything);

	[Test]
	public async Task RunAsync_AsksForEveryWantlistedAndBlocklistedTitleOnceWithEveryFieldShown()
	{
		var (wanted, both, blocked) = (MediaId.New(), MediaId.New(), MediaId.New());
		var deps = Dependencies.Create([wanted, both], [both, blocked]);

		await deps.Testee.RunAsync(CancellationToken.None);

		await Assert.That(deps.Asked.Single()).IsEquivalentTo([wanted, both, blocked]);
		deps.Media.GetDetailsAsync(Any(), EveryFieldShown, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RunAsync_ForMoreThanAPageWhileNoOtherJobIsQueued_FillsEveryPage()
	{
		var deps = Dependencies.Create([.. Enumerable.Range(0, 501).Select(_ => MediaId.New())], []);

		await deps.Testee.RunAsync(CancellationToken.None);

		await Assert.That(deps.Asked.Select(page => page.Count)).IsEquivalentTo([500, 1]);
		deps.Queue.EnqueueAsync(Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task RunAsync_WhenAnotherJobIsQueued_StopsAndRequeuesItself()
	{
		var deps = Dependencies.Create([.. Enumerable.Range(0, 501).Select(_ => MediaId.New())], []);
		deps.Jobs.AnyQueuedAsync(JobKind.DetailsFill, Any()).Returns(true);

		await deps.Testee.RunAsync(CancellationToken.None);

		await Assert.That(deps.Asked.Select(page => page.Count)).IsEquivalentTo([500]);
		deps.Queue.EnqueueAsync(Is<IJobRequest>(request => request is FillDetails), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RunAsync_AsksAtTheBackgroundPriority()
	{
		var deps = Dependencies.Create([MediaId.New()], []);
		var priority = LanePriority.Interactive;
		deps.Media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>()).Callback(() => priority = LanePriority.Current);

		await deps.Testee.RunAsync(CancellationToken.None);

		await Assert.That(priority.Class).IsEqualTo(LaneClass.Background);
	}

	[Test]
	public async Task HandleAsync_MarksItsCallsAsDetailsFill()
	{
		var deps = Dependencies.Create([MediaId.New()], []);
		var cause = UsageCause.Other;
		deps.Media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>()).Callback(() => cause = UsageCauses.Current.Cause);

		await new DetailsFillJobHandler(deps.Testee).HandleAsync(new FillDetails());

		await Assert.That(cause).IsEqualTo(UsageCause.DetailsFill);
	}

	private sealed record Dependencies(Mock<IMediaService> Media, Mock<IJobRepository> Jobs, Mock<IJobQueue> Queue, DetailsFill Testee)
	{
		public IEnumerable<IReadOnlyList<MediaId>> Asked
		{
			get
			{
				return [.. Mock.Invocations(Media).Select(call => call.Arguments[0]).OfType<IEnumerable<MediaId>>().Select(ids => (IReadOnlyList<MediaId>)[.. ids])];
			}
		}

		public static Dependencies Create(IReadOnlyList<MediaId> wanted, IReadOnlyList<MediaId> blocked)
		{
			var wantlist = IWantlistService.Mock();
			wantlist
				.GetAsync(Any())
				.Returns(new WantlistSnapshot([.. wanted.Select(id => new WantlistItem(id, DateTimeOffset.UnixEpoch, new ManualOrigin(ManualSource.Unknown)))]));
			var blocklist = IBlocklistService.Mock();
			blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([.. blocked.Select(id => new BlocklistItem(id, DateTimeOffset.UnixEpoch))]));
			var media = IMediaService.Mock();
			media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
			var jobs = IJobRepository.Mock();
			var queue = IJobQueue.Mock();
			var scopes = new ServiceCollection().AddSingleton(media.Object).AddSingleton(IUserScope.Mock().Object).BuildServiceProvider();
			var testee = new DetailsFill(
				wantlist.Object,
				blocklist.Object,
				jobs.Object,
				queue.Object,
				scopes.GetRequiredService<IServiceScopeFactory>(),
				new TestCurrentUser()
			);
			return new Dependencies(media, jobs, queue, testee);
		}
	}
}
