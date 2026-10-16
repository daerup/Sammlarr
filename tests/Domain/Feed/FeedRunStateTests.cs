using Domain.Feed;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedRunStateTests
{
	private static readonly Domain.Feed.Feed Comedy = TestFeeds.Feed(new Guid(1, 0, 0, new byte[8]));

	[Test]
	public async Task Failed_ForAPendingRun_IsFalse()
	{
		await Assert.That(TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Pending).Failed).IsFalse();
	}

	[Test]
	public async Task Failed_ForARunThatStoppedBeforeReadingATitle_IsTrue()
	{
		await Assert.That(TestFeeds.Run(Comedy, Guid.NewGuid(), Conclusiveness.Stopped).Failed).IsTrue();
	}
}
