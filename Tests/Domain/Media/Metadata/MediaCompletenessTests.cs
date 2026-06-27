namespace Domain.Tests.Media.Metadata;

public class MediaCompletenessTests
{
	[Test]
	public async Task IsComplete_WhenAllFieldsPresent_IsTrue()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie();

		await Assert.That(movie.IsComplete).IsTrue();
		await Assert.That(movie.MissingFields).IsEmpty();
	}

	[Test]
	public async Task MissingFields_WhenRuntimeMissing_ListsRuntime()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Runtime = null };

		await Assert.That(movie.IsComplete).IsFalse();
		await Assert.That(movie.MissingFields).Contains(nameof(PartialMovie.Runtime));
	}

	[Test]
	public async Task MissingFields_WhenGenresMissing_ListsGenres()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Genres = [] };

		await Assert.That(movie.MissingFields).Contains(nameof(IPartialMedia.Genres));
	}
}
