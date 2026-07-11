using Bogus;
using Contracts;
using Contracts.Auth;
using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Blacklist;
using Domain.Blacklist;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;

namespace Core.Tests.Blacklist;

public class BlacklistServiceTests
{
	private readonly Faker _faker = new();

	[Test]
	public async Task AddToBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository);

		await testee.AddToBacklistAsync(mediaId);

		repository.AddAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task RemoveFromBacklistAsync_WhenCalled_DelegatesToRepository()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var repository = IBlacklistRepository.Mock();
		var testee = CreateTestee(repository);

		await testee.RemoveFromBacklistAsync(mediaId);

		repository.RemoveAsync(Any(), Any()).WasCalled();
	}

	[Test]
	public async Task GetBlacklistItems_WhenEntriesExist_ReturnsItemsMappedFromMetadataProvider()
	{
		var addedAt = _faker.Date.PastOffset();
		var mediaId = MediaId.From(Guid.NewGuid());
		var entry = new BlacklistEntry(mediaId, addedAt);
		var detailResult = CreateMovieDetailResult(mediaId);

		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any()).Returns([entry]);

		IdentifiedMedia detail = detailResult;
		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(detail)));

		var testee = CreateTestee(repository, media.Object);

		var result = await testee.GetBlacklistItemsAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		var item = result[0];
		await Assert.That(item.Media.Id).IsEqualTo(mediaId);
		await Assert.That(item.Media is Movie movie && movie.Title == detailResult.Title).IsTrue();
		await Assert.That(item.AddedAt).IsEqualTo(addedAt);
	}

	[Test]
	public async Task GetBlacklistItems_WhenNoEntriesExist_ReturnsEmptyList()
	{
		var repository = IBlacklistRepository.Mock();
		repository.GetBlacklistedEntriesAsync(Any()).Returns(new List<BlacklistEntry>());

		var testee = CreateTestee(repository);

		var result = await testee.GetBlacklistItemsAsync().ToListAsync();

		await Assert.That(result).IsEmpty();
	}

	private BlacklistService CreateTestee(Mock<IBlacklistRepository>? repository = null, IMediaService? media = null)
	{
		var repo = repository ?? IBlacklistRepository.Mock();
		return new BlacklistService(repo.Object, media ?? DefaultMedia());
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

	private Movie CreateMovieDetailResult(MediaId mediaId) =>
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
