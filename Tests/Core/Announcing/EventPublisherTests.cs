using Core.Announcing;
using Domain.Announcing;
using Domain.Identity;
using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Core.Tests.Announcing;

public class EventPublisherTests
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Publish_ReachesEverySubscriber()
	{
		var testee = new EventPublisher();
		List<IEvent> first = [];
		List<IEvent> second = [];
		testee.Published += first.Add;
		testee.Published += second.Add;

		testee.Publish(Pushed());

		await Assert.That(first).Count().IsEqualTo(1);
		await Assert.That(second).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Publish_DoesNotFilterByType_SoASubscriberSeesEveryKindOfEvent()
	{
		var testee = new EventPublisher();
		List<IEvent> seen = [];
		testee.Published += seen.Add;

		testee.Publish(Pushed());
		testee.Publish(new UnrelatedEvent(Owner));

		await Assert.That(seen).Count().IsEqualTo(2);
	}

	private static WantlistOutboxEvent Pushed() => new(Owner, MediaId.From(Guid.NewGuid()), WantlistOutboxEventKind.Pushed);

	private sealed record UnrelatedEvent(UserId UserId) : IEvent;
}
