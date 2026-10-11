using API.Http;
using Shared.Concurrency;

namespace API.Tests.Http;

public class SourcePagesTests
{
	[Test]
	public async Task ReadAsync_ForAListPage_ReadsItAtSourcePriority()
	{
		var read = await SourcePages.ReadAsync(1, () => Task.FromResult(LanePriority.Current.Class));

		await Assert.That(read).IsEqualTo(LaneClass.Source);
	}
}
