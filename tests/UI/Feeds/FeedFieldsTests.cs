using BlazorBlueprint.Primitives.Filtering;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedFieldsTests
{
	[Test]
	public async Task Operators_ForEveryField_LeaveOutTheEmptyChecks()
	{
		var offered = FeedFields.All.SelectMany(field => field.Operators).Select(op => op.Value);

		await Assert.That(offered).DoesNotContain(FilterOperator.IsEmpty).And.DoesNotContain(FilterOperator.IsNotEmpty);
	}
}
