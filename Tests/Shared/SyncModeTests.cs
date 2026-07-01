using Contracts.Feed;
using TestInfrastructure;

namespace Shared.Tests;

public class SyncModeTests
{
	[Test]
	public async Task ToString_WhenManual_ReturnsManualLiteral()
	{
		var testee = new SyncMode(new Manual());

		var result = testee.ToString();

		await Assert.That(result).IsEqualTo("manual");
	}

	[Test]
	public async Task From_WhenManualString_ReturnsManualCase()
	{
		var result = SyncMode.From("manual");

		await Assert.That(result).IsOfUnionType<SyncMode, Manual>();
	}

	[Test]
	[Arguments("0 0 * * * *")]
	[Arguments("1 0 * * * *")]
	[Arguments("0 30 8 * * 1")]
	[Arguments("0 0 12 1 * *")]
	public async Task From_WhenCronExpression_ReturnsScheduledCaseWithExpression(string cron)
	{
		var result = SyncMode.From(cron);

		await Assert.That(result).IsOfUnionType<SyncMode, Scheduled>();
		await Assert.That(result.ToString()).IsEqualTo(cron);
	}

	[Test]
	[Arguments("lorem")]
	[Arguments("0 0 * @ * *")]
	[Arguments("0 * * * *")]
	public async Task From_WhenMalformedCron_ThrowsArgumentException(string cron)
	{
		await Assert.That(() => SyncMode.From(cron)).Throws<ArgumentException>();
	}
}
