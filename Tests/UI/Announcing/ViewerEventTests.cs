using Domain.Announcing;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Wantlist.Outbox;
using UI.Shared.Announcing;

namespace UI.Tests.Announcing;

public class ViewerEventTests
{
	private static readonly UserId Viewer = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Own_WhenTheEventIsTheViewersAndTheWantedKind_ReturnsIt()
	{
		var raised = Pushed(Viewer);

		await Assert.That(ViewerEvent.Own<WantlistOutboxEvent>(raised, Viewer)).IsEqualTo(raised);
	}

	[Test]
	public async Task Own_WhenTheEventIsAnotherKind_ReturnsNullSoThePageIgnoresIt()
	{
		var raised = new FeedRunFinishedEvent(Viewer, FeedId.From(Guid.NewGuid()));

		await Assert.That(ViewerEvent.Own<WantlistOutboxEvent>(raised, Viewer)).IsNull();
	}

	[Test]
	public async Task Own_WhenTheEventBelongsToAnotherUser_ReturnsNullSoNothingLeaksAcrossUsers()
	{
		var raised = Pushed(UserId.From(Guid.NewGuid()));

		await Assert.That(ViewerEvent.Own<WantlistOutboxEvent>(raised, Viewer)).IsNull();
	}

	[Test]
	public async Task Own_WhenNobodyIsSignedIn_ReturnsNull()
	{
		var raised = Pushed(Viewer);

		await Assert.That(ViewerEvent.Own<WantlistOutboxEvent>(raised, null)).IsNull();
	}

	private static WantlistOutboxEvent Pushed(UserId owner) => new(owner, MediaId.New(), WantlistOutboxEventKind.Pushed);
}
