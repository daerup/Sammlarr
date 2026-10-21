using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor.Filters;

namespace UI.Tests.Feeds.Filters;

public class FeedFieldsTests
{
	[Test]
	public async Task Operators_ForEveryField_LeaveOutTheEmptyChecks()
	{
		var offered = FeedFields.All.SelectMany(field => field.Operators).Select(op => op.Value);

		await Assert.That(offered).DoesNotContain(FilterOperator.IsEmpty).And.DoesNotContain(FilterOperator.IsNotEmpty);
	}

	[Test]
	public async Task SortOptions_OfferOnlyTheFieldsWithAMeaningfulOrder()
	{
		var sortable = FeedFields.SortOptions.Select(option => option.Value);

		await Assert
			.That(sortable)
			.IsEquivalentTo(
				[
					nameof(MediaQueryRow.ReleaseDate),
					nameof(MediaQueryRow.Rating),
					nameof(MediaQueryRow.Votes),
					nameof(MediaQueryRow.RuntimeMinutes),
					nameof(MediaQueryRow.Seasons),
					nameof(MediaQueryRow.Episodes),
				],
				CollectionOrdering.Matching
			);
	}
}
