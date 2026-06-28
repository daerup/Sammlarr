namespace Domain.Tests.Media;

public class MediaUnionsTests
{
	[Test]
	public async Task CombineIdentity_StampsCanonicalId()
	{
		MediaMatch media = MediaFakes.CompleteMovie();
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("9", MediaType.Movie)]);

		var combined = media.CombineIdentity(identity);

		await Assert.That(combined.Identity.Id).IsEqualTo(identity.Id);
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedGivenTypedIdentity_BecomesPartial()
	{
		MediaMatch unmatched = new UnmatchedMedia { Externals = [TmdbId.From("1", MediaType.Movie)], Title = "Dune" };
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("1", MediaType.Movie)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert.That(combined is PartialMovie movie && movie.Title == "Dune").IsTrue();
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedTypeUnknown_StaysUnmatched()
	{
		MediaMatch unmatched = new UnmatchedMedia { Externals = [MdblistId.From("x", MediaType.Unknown)] };
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [MdblistId.From("x", MediaType.Unknown)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert.That(combined is UnmatchedMedia).IsTrue();
	}
}
