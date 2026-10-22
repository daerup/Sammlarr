namespace UI.Tests.Feeds.Filters;

public class FilterDepthTests
{
	[Test]
	public async Task Stripe_ForEachShippedLevel_IsDistinct()
	{
		var stripes = FeedFilterStub.Shipped.Levels.Select(level => level.Stripe);

		await Assert.That(stripes.Distinct()).Count().IsEqualTo(FeedFilterStub.Shipped.Levels.Count);
	}
}
