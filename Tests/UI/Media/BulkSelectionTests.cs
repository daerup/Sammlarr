using UI.Shared.Media;

namespace UI.Tests.Media;

public class BulkSelectionTests
{
	private static readonly string[] Page = ["comedy", "documentaries", "digital", "pirated", "trending"];

	private BulkSelection<string> Testee { get; } = new();

	[Test]
	public async Task Click_OnTheSameItemTwice_SelectsItThenLetsItGo()
	{
		Testee.Click("comedy", extend: false, Page);
		await Assert.That(Testee.Contains("comedy")).IsTrue();

		Testee.Click("comedy", extend: false, Page);
		await Assert.That(Testee.Count).IsEqualTo(0);
	}

	[Test]
	public async Task Click_WithShiftAfterSelecting_SelectsEverythingBetween()
	{
		Testee.Click("documentaries", extend: false, Page);

		Testee.Click("pirated", extend: true, Page);

		await Assert.That(Testee.Keys).IsEquivalentTo(["documentaries", "digital", "pirated"]);
	}

	[Test]
	public async Task Click_WithShiftAfterDeselecting_DeselectsEverythingBetween()
	{
		Testee.SelectAll(Page);
		Testee.Click("trending", extend: false, Page);

		Testee.Click("digital", extend: true, Page);

		await Assert.That(Testee.Keys).IsEquivalentTo(["comedy", "documentaries"]);
	}

	[Test]
	public async Task Click_WithShiftWhenTheAnchorIsNotOnThePage_JustTogglesTheItem()
	{
		Testee.Click("from-another-page", extend: false, ["from-another-page"]);

		Testee.Click("digital", extend: true, Page);

		await Assert.That(Testee.Keys).IsEquivalentTo(["from-another-page", "digital"]);
	}

	[Test]
	public async Task SelectAll_ReplacesWhateverWasSelectedBefore()
	{
		Testee.Click("comedy", extend: false, Page);

		Testee.SelectAll(["pirated", "trending"]);

		await Assert.That(Testee.Keys).IsEquivalentTo(["pirated", "trending"]);
	}
}
