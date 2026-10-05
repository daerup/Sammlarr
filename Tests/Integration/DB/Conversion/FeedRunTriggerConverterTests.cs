using DB.Conversion.Feed;
using Domain.Feed;
using TestInfrastructure;

namespace Integration.Tests.DB.Conversion;

public class FeedRunTriggerConverterTests
{
	private readonly FeedRunTriggerConverter _testee = new();

	[Test]
	public async Task ConvertToProvider_WhenManual_WritesTheManualLiteral()
	{
		var stored = _testee.ConvertToProvider(new FeedRunTrigger(new Manual()));

		await Assert.That(stored).IsEqualTo("manual");
	}

	[Test]
	public async Task ConvertFromProvider_WhenManualLiteral_ReturnsManual()
	{
		var restored = (FeedRunTrigger)_testee.ConvertFromProvider("manual")!;

		await Assert.That(restored).IsOfUnionType<FeedRunTrigger, Manual>();
	}

	[Test]
	[Arguments("0 0 * * * *")]
	[Arguments("1 0 * * * *")]
	[Arguments("0 30 8 * * 1")]
	[Arguments("0 0 12 1 * *")]
	public async Task Convert_WhenScheduled_RoundTripsTheCronExpression(string cron)
	{
		var original = new FeedRunTrigger(new Scheduled(cron));

		var restored = (FeedRunTrigger)_testee.ConvertFromProvider(_testee.ConvertToProvider(original))!;

		await Assert.That(restored).IsOfUnionType<FeedRunTrigger, Scheduled>();
		await Assert.That(_testee.ConvertToProvider(restored)).IsEqualTo(cron);
	}

	[Test]
	[Arguments("lorem")]
	[Arguments("0 0 * @ * *")]
	[Arguments("0 * * * *")]
	public async Task ConvertFromProvider_WhenMalformedCron_Throws(string cron)
	{
		await Assert.That(() => _testee.ConvertFromProvider(cron)).Throws<ArgumentException>();
	}
}
