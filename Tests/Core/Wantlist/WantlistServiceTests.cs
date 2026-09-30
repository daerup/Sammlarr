using Bogus;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Core.Wantlist;
using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;

namespace Core.Tests.Wantlist;

public class WantlistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task GetWantlistItemsAsync_IncludeRemoved_MergesRemovedItemsAtTheirOriginalAddedAt()
	{
		var activeId = MediaId.From(Guid.NewGuid());
		var removedId = MediaId.From(Guid.NewGuid());
		var activeAddedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
		var removedAddedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

		var repository = IWantlistRepository.Mock();
		repository.GetEntriesAsync(Any()).Returns([new WantlistItem(activeId, activeAddedAt, new ManualOrigin(ManualSource.Search))]);

		var outbox = IWantlistOutbox.Mock();
		outbox
			.GetAsync(Any())
			.Returns([new WantlistOutboxEntry(removedId, WantlistOperationAction.Remove, WantlistOutboxState.Pending, 0) { AddedAt = removedAddedAt }]);

		IdentifiedMedia active = CreateMovie(activeId);
		IdentifiedMedia removed = CreateMovie(removedId);
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(active), Success.Of(removed)));

		var testee = CreateTestee(repository, outbox, media.Object);

		var result = await testee.GetWantlistItemsAsync(WantlistQueryOptions.WithRemoved).ToListAsync();

		await Assert.That(result.Select(item => item.Media.Id)).Contains(removedId);
		var removedItem = result.First(item => item.Media.Id == removedId);
		await Assert.That(removedItem.AddedAt).IsEqualTo(removedAddedAt);
		await Assert.That(result[0].Media.Id).IsEqualTo(activeId);
	}

	private static WantlistService CreateTestee(Mock<IWantlistRepository> repository, Mock<IWantlistOutbox> outbox, IMediaService media)
	{
		var mediaRepository = IMediaRepository.Mock();
		var configRepository = IProviderConfigService.Mock();
		return new WantlistService(repository.Object, outbox.Object, media, mediaRepository.Object, configRepository.Object);
	}

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
			await Task.CompletedTask;
		}
	}

	private Movie CreateMovie(MediaId mediaId) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, [PlexId.From(_faker.Random.AlphaNumeric(10), MediaType.Movie)]),
			Title = _faker.Lorem.Sentence(2),
			Tagline = _faker.Company.CatchPhrase(),
			Summary = _faker.Lorem.Paragraph(),
			PosterUrl = _faker.Internet.Url(),
			BackdropUrl = _faker.Internet.Url(),
			Runtime = TimeSpan.FromMinutes(_faker.Random.Int(60, 180)),
			ReleaseDate = _faker.Date.PastDateOnly(),
			Genres = [],
		};
}
