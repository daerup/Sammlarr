using TUnit.Assertions.Enums;
using UI.Shared.Bars;

namespace UI.Tests.Bars;

public class StackedBarViewTests
{
	private static readonly BarPaint Unnamed = new(new None(), BarPattern.Solid);

	[Test]
	public async Task Colors_WithoutANamedColour_TurnTheWheelFromTheAccent()
	{
		var colors = StackedBarView.Colors([Unnamed, Unnamed]);

		await Assert
			.That(Css(colors))
			.IsEquivalentTo(
				[
					"oklch(from var(--chart-1) var(--bar-auto-l) var(--bar-auto-c) calc(h + 0))",
					"oklch(from var(--chart-1) var(--bar-auto-l2) var(--bar-auto-c) calc(h + 137.5))",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Colors_AfterANamedColour_StartOneStepAwayFromIt()
	{
		var colors = StackedBarView.Colors([new(Some.Of("#F5C518"), BarPattern.Solid), Unnamed, Unnamed]);

		await Assert
			.That(Css(colors))
			.IsEquivalentTo(
				[
					"#F5C518",
					"oklch(from #F5C518 var(--bar-auto-l) var(--bar-auto-c) calc(h + 137.5))",
					"oklch(from #F5C518 var(--bar-auto-l2) var(--bar-auto-c) calc(h + 275))",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Colors_APatternedPart_TakesNoColourAndNoStep()
	{
		var colors = StackedBarView.Colors([Unnamed, new(new None(), BarPattern.Hatched), Unnamed]);

		await Assert
			.That(Css(colors))
			.IsEquivalentTo(
				[
					"oklch(from var(--chart-1) var(--bar-auto-l) var(--bar-auto-c) calc(h + 0))",
					"none",
					"oklch(from var(--chart-1) var(--bar-auto-l2) var(--bar-auto-c) calc(h + 137.5))",
				],
				CollectionOrdering.Matching
			);
	}

	private static IEnumerable<string> Css(IReadOnlyList<Optional<string>> colors) => colors.Select(color => color.GetValueOr("none"));
}
