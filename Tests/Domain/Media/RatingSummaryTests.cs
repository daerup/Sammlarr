using Domain.Media;
using Domain.Providers;

namespace Domain.Tests.Media;

public class RatingSummaryTests
{
	[Test]
	public async Task From_WhenSources_ComputesAverage()
	{
		var summary = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8f)]);

		await Assert.That(summary.Average).IsEqualTo(8f);
		await Assert.That(summary.Sources.Count).IsEqualTo(1);
	}

	[Test]
	public async Task From_WhenEmpty_IsNone()
	{
		var summary = RatingSummary.From([]);

		await Assert.That(summary).IsEqualTo(RatingSummary.None);
		await Assert.That(summary.Average).IsNull();
	}

	[Test]
	public async Task Equals_WhenSameSources_AreEqual()
	{
		var first = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8f)]);
		var second = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8f)]);

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}
}
