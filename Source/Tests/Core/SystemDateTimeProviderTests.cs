namespace Core.Tests;

public class SystemDateTimeProviderTests
{
	[Test]
	public async Task Now_WhenAccessed_ReturnsCurrentUtcTime()
	{
		var testee = new SystemDateTimeProvider();
		var before = DateTimeOffset.UtcNow;

		var result = testee.Now;

		var after = DateTimeOffset.UtcNow;
		await Assert.That(result).IsGreaterThanOrEqualTo(before);
		await Assert.That(result).IsLessThanOrEqualTo(after);
		await Assert.That(result.Offset).IsEqualTo(TimeSpan.Zero);
	}

	[Test]
	public async Task Now_WhenAccessedMultipleTimes_ReturnsNonDecreasingValues()
	{
		var testee = new SystemDateTimeProvider();

		var first = testee.Now;
		var second = testee.Now;

		await Assert.That(second).IsGreaterThanOrEqualTo(first);
	}
}
