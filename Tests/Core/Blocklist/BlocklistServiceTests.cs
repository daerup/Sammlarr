using Contracts.Blocklist;
using Contracts.Media;
using Core.Blocklist;
using Domain.Blocklist;
using Domain.Media;

namespace Core.Tests.Blocklist;

public class BlocklistServiceTests
{
	[Test]
	public async Task GetBlocklistItems_WhenNoEntriesExist_ReturnsEmptyList()
	{
		var repository = IBlocklistRepository.Mock();
		repository.GetBlocklistedEntriesAsync(Any()).Returns(new List<BlocklistEntry>());

		var testee = new BlocklistService(repository.Object, DefaultMedia());

		var result = await testee.GetBlocklistItemsAsync().ToListAsync();

		await Assert.That(result).IsEmpty();
	}

	private static IMediaService DefaultMedia()
	{
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>());
		return media.Object;
	}

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}
}
