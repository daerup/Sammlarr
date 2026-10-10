using Contracts.Media;
using Core.Media;
using Domain.Media;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Media;

public class MediaWriteBufferTests
{
	private static readonly DateTimeOffset RefreshedAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

	private readonly Mock<IMediaRepository> _repository = IMediaRepository.Mock();

	[Test]
	public async Task DisposeAsync_ForDetailsAddedTwiceForOneTitle_FlushesTheLatestOnce()
	{
		var title = MediaId.New();

		await using (var testee = new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance))
		{
			testee.Add(title, Cached("First"));
			testee.Add(title, Cached("Second"));
		}

		_repository
			.UpsertDetailsAsync(
				Is<IReadOnlyDictionary<MediaId, CachedDetails>>(details => details is { Count: 1 } && details[title].Details.Title == "Second"),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task DisposeAsync_WhenNothingWasAdded_WritesNothing()
	{
		await using (new MediaWriteBuffer(_repository.Object, NullLogger<MediaWriteBuffer>.Instance)) { }

		_repository.UpsertDetailsAsync(Any<IReadOnlyDictionary<MediaId, CachedDetails>>(), Any()).WasNeverCalled();
	}

	private static CachedDetails Cached(string title) => new(new MovieDetails { Title = title }, RefreshedAt, RefreshedAt, []);
}
