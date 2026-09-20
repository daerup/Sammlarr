using Domain.Feed;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class MirrorSwitchTests
{
	[Test]
	public async Task NeedsConfirmation_WhenAppendBecomesMirrorWithItemsExposed_IsTrue()
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(new Append(), new Mirror(3), exposure: 1)).IsTrue();
	}

	[Test]
	public async Task NeedsConfirmation_WhenNothingIsExposed_IsFalse()
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(new Append(), new Mirror(3), exposure: 0)).IsFalse();
	}

	[Test]
	public async Task NeedsConfirmation_WhenTheFeedWasAlreadyMirroring_IsFalse()
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(new Mirror(3), new Mirror(5), exposure: 4)).IsFalse();
	}

	[Test]
	public async Task NeedsConfirmation_WhenMirrorBecomesAppend_IsFalse()
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(new Mirror(3), new Append(), exposure: 4)).IsFalse();
	}

	[Test]
	public async Task NeedsConfirmation_WhenTheFeedStaysOnAppend_IsFalse()
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(new Append(), new Append(), exposure: 4)).IsFalse();
	}

	[Test]
	public async Task Warning_ForASingleTitle_AgreesInTheSingular()
	{
		var warning = MirrorSwitch.Warning(exposure: 1, threshold: 3);

		await Assert.That(warning).IsEqualTo("1 has already left the list, and is deleted after 3 runs without it.");
	}

	[Test]
	public async Task Warning_ForSeveralTitles_AgreesInThePlural()
	{
		var warning = MirrorSwitch.Warning(exposure: 4, threshold: 3);

		await Assert.That(warning).IsEqualTo("4 have already left the list, and are deleted after 3 runs without them.");
	}

	[Test]
	public async Task Warning_WhenThereIsNoGracePeriod_SaysTheNextRunRatherThanCountingOne()
	{
		var warning = MirrorSwitch.Warning(exposure: 2, threshold: 1);

		await Assert.That(warning).IsEqualTo("2 have already left the list, and are deleted on the next run.");
	}

	[Test]
	public async Task Heading_Always_SaysOnlyThisFeedsTitlesAreAffected()
	{
		await Assert.That(MirrorSwitch.Heading).Contains("titles this feed added");
	}
}
