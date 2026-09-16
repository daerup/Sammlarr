using Contracts.Blacklist;
using Contracts.Media;
using Core.Blacklist;
using Domain.Blacklist;
using Domain.Media;

namespace Core.Tests.Blacklist;

public class BlacklistServiceTests
{
	[Test]
	public async Task GetBlacklistItems_WhenNoEntriesExist_ReturnsEmptyList()
	{
		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any()).Returns(new List<BlacklistEntry>());

		var testee = new BlacklistService(repository.Object, DefaultMedia());

		var result = await testee.GetBlacklistItemsAsync().ToListAsync();

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
