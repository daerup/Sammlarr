using CsCheck;
using Domain.Usage;
using TestInfrastructure;

namespace Domain.Tests.Usage;

public class DurationBucketsTests
{
	[Test]
	public async Task Percentile_InsideABucket_ReadsLinearlyBetweenItsBounds()
	{
		var counts = new Dictionary<int, long> { [100] = 10 };

		await Assert.That(DurationBuckets.Percentile(counts, 0.5)).IsSome().And.IsEqualTo(87.5);
	}

	[Test]
	public async Task Percentile_OfAnEmptyHistogram_IsNone()
	{
		await Assert.That(DurationBuckets.Percentile(new Dictionary<int, long>(), 0.95)).IsNone();
	}

	[Test]
	public async Task Percentile_InTheLastBucket_IsItsLowerBound()
	{
		var counts = new Dictionary<int, long> { [50] = 1, [DurationBuckets.Over] = 9 };

		await Assert.That(DurationBuckets.Percentile(counts, 0.95)).IsSome().And.IsEqualTo(10_000);
	}

	[Test]
	public void Percentile_ForAnyHistogram_GrowsWithQ()
	{
		Gen.Select(Gen.Long[0, 50].Array[DurationBuckets.UpToMs.Count], Gen.Double[0, 1], Gen.Double[0, 1])
			.Sample(
				(counts, first, second) =>
				{
					var histogram = DurationBuckets.UpToMs.Zip(counts).ToDictionary(pair => pair.First, pair => pair.Second);
					var low = DurationBuckets.Percentile(histogram, Math.Min(first, second)).GetValueOr(0);
					var high = DurationBuckets.Percentile(histogram, Math.Max(first, second)).GetValueOr(0);
					return low <= high + 1e-9;
				},
				iter: 10_000
			);
	}
}
