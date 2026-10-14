using Contracts.Media;
using Domain.Media;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaDetailsLoadingTests
{
	private static readonly MediaId Id = MediaId.From(new Guid(41, 0, 0, new byte[8]));

	private readonly Mock<IMediaService> _media = IMediaService.Mock();

	private IMediaService Testee => _media.Object;

	[Test]
	public async Task GetDetailsByIdAsync_WhenAnIdComesBackTwice_AsksForNoExtraFieldsAndKeepsTheLastAnswer()
	{
		Result<MatchedMedia>[] answers = [Success.Of(Titled("First")), Success.Of(Titled("Second"))];
		_media.GetDetailsAsync(Any(), FieldNeeds.None, Any()).Returns(answers.ToAsyncEnumerable());

		var found = await Testee.GetDetailsByIdAsync([Id]);

		await Assert.That(found[Id].Details.Title).IsEqualTo("Second");
	}

	[Test]
	public async Task GetDetailsByIdAsync_WhenAnIdCannotBeLoaded_LeavesItOut()
	{
		Result<MatchedMedia>[] answers = [Error.From("gone")];
		_media.GetDetailsAsync(Any(), Any(), Any()).Returns(answers.ToAsyncEnumerable());

		var found = await Testee.GetDetailsByIdAsync([Id]);

		await Assert.That(found).IsEmpty();
	}

	private static MatchedMedia Titled(string title) => new(new MediaIdentity(Id, []), new MovieDetails { Title = title });
}
