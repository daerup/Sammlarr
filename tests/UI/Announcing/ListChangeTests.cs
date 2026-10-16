using Domain.Media;
using UI.Shared.Announcing;

namespace UI.Tests.Announcing;

public class ListChangeTests
{
	private static readonly MediaId Shown = MediaId.New();

	private static readonly MediaId Unseen = MediaId.New();

	private static bool Shows(MediaId id) => id == Shown;

	[Test]
	public async Task Differs_WhenATitleNotShownIsAdded_IsTrue()
	{
		await Assert.That(ListChange.Differs(Shows, [Unseen], [])).IsTrue();
	}

	[Test]
	public async Task Differs_WhenAShownTitleIsRemoved_IsTrue()
	{
		await Assert.That(ListChange.Differs(Shows, [], [Shown])).IsTrue();
	}

	[Test]
	public async Task Differs_WhenTheChangeIsAlreadyShown_IsFalse()
	{
		await Assert.That(ListChange.Differs(Shows, [Shown], [Unseen])).IsFalse();
	}
}
