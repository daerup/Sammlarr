using Core.Usage;

namespace Core.Tests.Usage;

public class MediansTests
{
	[Test]
	[Arguments(new double[] { }, 0)]
	[Arguments(new double[] { 7 }, 7)]
	[Arguments(new double[] { 30, 10, 1000 }, 30)]
	[Arguments(new double[] { 4, 1, 3, 2 }, 2.5)]
	public async Task Of_AnyRuns_IsTheMiddleValue(double[] values, double median)
	{
		await Assert.That(Medians.Of(values)).IsEqualTo(median);
	}
}
