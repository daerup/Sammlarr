using UI.Shared.Formatting;

namespace UI.Tests.Formatting;

public class SharesTests
{
	[Test]
	[Arguments(0L, 140L, "0.0%")]
	[Arguments(1L, 8287L, "<0.1%")]
	[Arguments(43L, 14483L, "0.3%")]
	[Arguments(5L, 0L, "0%")]
	public async Task Of_APart_ReadsLessThanATenthWhenItWouldRoundToNothing(long part, long total, string share)
	{
		await Assert.That(Shares.Of(part, total)).IsEqualTo(share);
	}
}
