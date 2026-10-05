using DB.Conversion.Feed;
using Domain.Feed;
using TestInfrastructure;

namespace DB.Tests.Conversion;

public class FeedStrategyConverterTests
{
	private readonly FeedStrategyConverter _testee = new();

	[Test]
	[Arguments(1)]
	[Arguments(3)]
	[Arguments(42)]
	public async Task Convert_WhenMirroring_RoundTripsTheAbsenceThreshold(int threshold)
	{
		var original = new FeedStrategy(new Mirror(threshold));

		var restored = (FeedStrategy)_testee.ConvertFromProvider(_testee.ConvertToProvider(original))!;

		await Assert.That(restored).IsEqualTo(original);
		await Assert.That(restored).IsOfUnionType<FeedStrategy, Mirror>();
	}

	[Test]
	public async Task Convert_WhenAppending_RoundTripsAppend()
	{
		var original = new FeedStrategy(new Append());

		var restored = (FeedStrategy)_testee.ConvertFromProvider(_testee.ConvertToProvider(original))!;

		await Assert.That(restored).IsEqualTo(original);
		await Assert.That(restored).IsOfUnionType<FeedStrategy, Append>();
	}

	[Test]
	public async Task ConvertToProvider_WhenMirroring_WritesTheThresholdIntoTheStoredText()
	{
		var stored = _testee.ConvertToProvider(new FeedStrategy(new Mirror(7)));

		await Assert.That(stored).IsEqualTo("mirror:7");
	}

	[Test]
	public async Task ConvertToProvider_WhenAppending_WritesAppend()
	{
		var stored = _testee.ConvertToProvider(new FeedStrategy(new Append()));

		await Assert.That(stored).IsEqualTo("append");
	}
}
