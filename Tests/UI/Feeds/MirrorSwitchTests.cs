using Domain.Feed;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class MirrorSwitchTests
{
	public static IEnumerable<Func<(FeedStrategy Stored, FeedStrategy Pending, Optional<int> Exposure, bool Expected)>> Switches()
	{
		return
		[
			() => (new Append(), new Mirror(3), Some.Of(1), true),
			() => (new Append(), new Mirror(3), new None(), true),
			() => (new Append(), new Mirror(3), Some.Of(0), false),
			() => (new Mirror(3), new Mirror(5), Some.Of(4), false),
			() => (new Mirror(3), new Append(), Some.Of(4), false),
			() => (new Append(), new Append(), Some.Of(4), false),
		];
	}

	[Test]
	[MethodDataSource(nameof(Switches))]
	public async Task NeedsConfirmation_AsksOnlyWhenAppendBecomesMirrorWithTitlesExposedOrAnUnknownCount(
		FeedStrategy stored,
		FeedStrategy pending,
		Optional<int> exposure,
		bool expected
	)
	{
		await Assert.That(MirrorSwitch.NeedsConfirmation(stored, pending, exposure)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(1, 3, "1 has already left the list, and is deleted after 3 runs without it.")]
	[Arguments(4, 3, "4 have already left the list, and are deleted after 3 runs without them.")]
	[Arguments(2, 1, "2 have already left the list, and are deleted on the next run.")]
	public async Task Warning_ForAKnownCount_AgreesInNumberAndSaysTheNextRunWithoutAGracePeriod(int exposure, int threshold, string expected)
	{
		await Assert.That(MirrorSwitch.Warning(Some.Of(exposure), threshold)).IsEqualTo(expected);
	}

	[Test]
	public async Task Warning_WithAnUnknownCount_SaysItIsNotKnownYet()
	{
		var warning = MirrorSwitch.Warning(exposure: new None(), threshold: 3);

		await Assert
			.That(warning)
			.IsEqualTo("The preview hasn't finished, so it can't tell yet how many titles have already left the list. Those are deleted after 3 runs without them.");
	}
}
