using Domain.Notifications;

namespace Domain.Tests.Notifications;

public class PushoverPriorityTests
{
	[Test]
	public async Task Ascending_OrdersByTheNumericValue_BecauseGetValuesPutsNegativesLast()
	{
		await Assert
			.That(PushoverPriorities.Ascending)
			.IsEquivalentTo(
				(IReadOnlyList<PushoverPriority>)
					[PushoverPriority.Lowest, PushoverPriority.Low, PushoverPriority.Normal, PushoverPriority.High, PushoverPriority.Emergency]
			);
	}

	[Test]
	public async Task Ascending_CoversEveryPriority()
	{
		await Assert.That(PushoverPriorities.Ascending).Count().IsEqualTo(Enum.GetValues<PushoverPriority>().Length);
	}
}
