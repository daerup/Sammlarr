using Domain.Usage;

namespace Domain.Tests.Usage;

public class UsageOutcomesTests
{
	[Test]
	[Arguments(200, 0, UsageOutcome.Answered)]
	[Arguments(404, 0, UsageOutcome.Answered)]
	[Arguments(200, 1, UsageOutcome.AnsweredAfter429)]
	[Arguments(429, 2, UsageOutcome.GaveUpOn429)]
	[Arguments(429, 0, UsageOutcome.GaveUpOn429)]
	[Arguments(503, 0, UsageOutcome.Failed)]
	[Arguments(401, 0, UsageOutcome.Failed)]
	public async Task Classify_AFinalStatus_EndsInItsOutcome(int status, int rateLimited, UsageOutcome expected)
	{
		await Assert.That(UsageOutcomes.Classify(Some.Of(status), UsageFault.None, rateLimited)).IsEqualTo(expected);
	}

	[Test]
	[Arguments(UsageFault.TimedOut, 1, UsageOutcome.GaveUpOn429)]
	[Arguments(UsageFault.TimedOut, 0, UsageOutcome.Failed)]
	[Arguments(UsageFault.Other, 1, UsageOutcome.Failed)]
	[Arguments(UsageFault.Cancelled, 0, UsageOutcome.Cancelled)]
	[Arguments(UsageFault.Cancelled, 1, UsageOutcome.Cancelled)]
	public async Task Classify_WithoutAnAnswer_EndsInItsOutcome(UsageFault fault, int rateLimited, UsageOutcome expected)
	{
		await Assert.That(UsageOutcomes.Classify(new None(), fault, rateLimited)).IsEqualTo(expected);
	}
}
