using Domain.Notifications;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Notifications;

public class PushoverPriorityTests
{
	[Test]
	public async Task Ascending_OrdersByTheNumericValue_BecauseGetValuesPutsNegativesLast()
	{
		await Assert
			.That(PushoverPriorities.Ascending)
			.IsEquivalentTo(
				[PushoverPriority.Lowest, PushoverPriority.Low, PushoverPriority.Normal, PushoverPriority.High, PushoverPriority.Emergency],
				CollectionOrdering.Matching
			);
	}
}
