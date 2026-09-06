using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaUnionsTests
{
	[Test]
	public async Task CombineIdentity_StampsCanonicalId()
	{
		MediaLookup media = MediaFakes.CompleteMovie().ToMatch();
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("9", MediaType.Movie)]);

		var combined = media.CombineIdentity(identity);

		await Assert.That(combined.Identity.Id).IsEqualTo(identity.Id);
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedGivenTypedIdentity_BecomesPartial()
	{
		MediaLookup unmatched = new UnmatchedMedia { Externals = [TmdbId.From("1", MediaType.Movie)], Title = "Dune" };
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("1", MediaType.Movie)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert.That(combined).IsOfUnionType<MediaLookup, PartialMovie>().And.Multiple(async movie => await Assert.That(movie.Title).IsEqualTo("Dune"));
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedTypeUnknown_StaysUnmatched()
	{
		MediaLookup unmatched = new UnmatchedMedia { Externals = [MdblistId.From("x", MediaType.Unknown)] };
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [MdblistId.From("x", MediaType.Unknown)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert.That(combined).IsOfUnionType<MediaLookup, UnmatchedMedia>();
	}
}
