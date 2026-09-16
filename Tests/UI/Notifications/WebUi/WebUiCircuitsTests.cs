using Domain.Identity;
using Domain.Notifications;
using UI.Shared.Notifications.WebUi;
using UI.Shared.Services;

namespace UI.Tests.Notifications.WebUi;

public class WebUiCircuitsTests
{
	private static readonly Toast Toast = new(NotificationLevel.Warning, "Rate limited", "Plex is throttling");

	private readonly WebUiCircuits testee = new();
	private readonly List<Toast> shown = [];

	[Test]
	public async Task Show_WhenACircuitBelongsToTheUser_ShowsTheToast()
	{
		var user = UserId.New();
		testee.Register(user, shown.Add);

		testee.Show(user, Toast);

		await Assert.That(shown).HasSingleItem();
		await Assert.That(shown[0]).IsEqualTo(Toast);
	}

	[Test]
	public async Task Show_WhenTheCircuitBelongsToAnotherUser_ShowsNothing()
	{
		testee.Register(UserId.New(), shown.Add);

		testee.Show(UserId.New(), Toast);

		await Assert.That(shown).IsEmpty();
	}

	[Test]
	public async Task Show_WhenTheUserHasSeveralCircuits_ShowsTheToastInEachOfThem()
	{
		var user = UserId.New();
		testee.Register(user, shown.Add);
		testee.Register(user, shown.Add);

		testee.Show(user, Toast);

		await Assert.That(shown).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Show_WhenTheRegistrationIsDisposed_ShowsNothing()
	{
		var user = UserId.New();
		testee.Register(user, shown.Add).Dispose();

		testee.Show(user, Toast);

		await Assert.That(shown).IsEmpty();
	}

	[Test]
	public async Task Show_WhenOneOfSeveralCircuitsIsDisposed_ShowsTheToastInTheRemainingOne()
	{
		var user = UserId.New();
		testee.Register(user, shown.Add).Dispose();
		testee.Register(user, shown.Add);

		testee.Show(user, Toast);

		await Assert.That(shown).HasSingleItem();
	}

	[Test]
	public async Task Show_WhenNoCircuitIsOpen_ShowsNothing()
	{
		testee.Show(UserId.New(), Toast);

		await Assert.That(shown).IsEmpty();
	}
}
