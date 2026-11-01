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

	[Test]
	[Arguments(null)]
	[Arguments("usage")]
	[Arguments("nowhere")]
	public async Task SectionFor_ASlugTheRoleCannotOpen_FallsBackToTheFirstSection(string? slug)
	{
		await Assert.That(AppSections.SectionFor(UserRole.Member, slug).Label).IsEqualTo("Providers");
	}

	[Test]
	public async Task SectionFor_ASlugInAnyCase_OpensItsSection()
	{
		await Assert.That(AppSections.SectionFor(UserRole.Member, "NOTIFICATIONS").Label).IsEqualTo("Notifications");
	}
}
