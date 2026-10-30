using Domain.Identity;
using TUnit.Assertions.Enums;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class AppSectionsTests
{
	[Test]
	public async Task SettingsFor_AMember_ListsTheSectionsForEveryoneAndAccountInOrder()
	{
		var labels = AppSections.SettingsFor(UserRole.Member).Select(section => section.Label);

		await Assert.That(labels).IsEquivalentTo(["Providers", "Ratings", "Notifications", "Account", "About"], CollectionOrdering.Matching);
	}
}
