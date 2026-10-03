using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaUnionsTests
{
	[Test]
	public async Task CombineIdentity_WhenNeitherSideHasExternals_StaysUnmatched()
	{
		MediaLookup media = new UnmatchedMedia([], new UnknownDetails());

		var combined = media.CombineIdentity(new MediaIdentity(MediaId.From(Guid.NewGuid()), []));

		await Assert.That(combined).IsOfUnionType<MediaLookup, UnmatchedMedia>();
	}

	[Test]
	public async Task CombineIdentity_StampsCanonicalId()
	{
		MediaLookup media = MediaFakes.CompleteMovie();
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("9", MediaType.Movie)]);

		var combined = media.CombineIdentity(identity);

		await Assert.That(combined.Identity.Id).IsEqualTo(identity.Id);
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedGivenTypedIdentity_BecomesIdentified()
	{
		MediaLookup unmatched = new UnmatchedMedia([TmdbId.From("1", MediaType.Movie)], new MovieDetails { Title = "Dune" });
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("1", MediaType.Movie)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert
			.That(combined)
			.IsOfUnionType<MediaLookup, IdentifiedMedia>()
			.And.Multiple(async identified => await Assert.That(identified.Details).IsEqualTo(new MovieDetails { Title = "Dune" }));
	}

	[Test]
	public async Task CombineIdentity_WhenUnmatchedTypeUnknown_StaysUnmatched()
	{
		MediaLookup unmatched = new UnmatchedMedia([MdblistId.From("x", MediaType.Unknown)], new UnknownDetails());
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [MdblistId.From("x", MediaType.Unknown)]);

		var combined = unmatched.CombineIdentity(identity);

		await Assert.That(combined).IsOfUnionType<MediaLookup, UnmatchedMedia>();
	}
}
