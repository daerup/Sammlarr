using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class FoundMediaTests
{
	private static readonly MediaIdentity Movie = new(MediaId.From(Guid.NewGuid()), [PlexId.From("plex-1", MediaType.Movie)]);

	[Test]
	public async Task LookUpAs_WhenTheTypeMatches_KeepsTheIdentitysMediaIdAndAddsWhatTheProviderFound()
	{
		var tmdb = TmdbId.From("27205", MediaType.Movie);
		var found = new FoundMedia([tmdb], new MovieDetails { Title = "Inception" });

		await Assert
			.That(found.LookUpAs(Movie))
			.IsOfUnionType<MediaLookup, IdentifiedMedia>()
			.And.Multiple(async identified =>
			{
				await Assert.That(identified.Identity.Id).IsEqualTo(Movie.Id);
				await Assert.That(identified.Identity.Externals).IsEquivalentTo([.. Movie.Externals, tmdb]);
				await Assert.That(identified.Details).IsEqualTo(new MovieDetails { Title = "Inception" });
			});
	}

	[Test]
	public async Task LookUpAs_WhenTheProviderFoundAPerson_StaysUnmatched()
	{
		var found = new FoundMedia([TmdbId.From("525", MediaType.Unknown)], new UnknownDetails { Title = "Christopher Nolan" });

		await Assert
			.That(found.LookUpAs(new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("525", MediaType.Unknown)])))
			.IsOfUnionType<MediaLookup, UnmatchedMedia>();
	}

	[Test]
	public async Task LookUpAs_WhenTheTypeDiffers_StaysUnmatchedWithWhatWasFound()
	{
		var show = TmdbId.From("1396", MediaType.Show);
		var found = new FoundMedia([show], new ShowDetails { Title = "Breaking Bad" });

		await Assert
			.That(found.LookUpAs(Movie))
			.IsOfUnionType<MediaLookup, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched).IsEqualTo(new UnmatchedMedia([show], new ShowDetails { Title = "Breaking Bad" })));
	}

	[Test]
	public async Task Fits_WhenAnIdIsOfAnotherType_IsFalse()
	{
		var found = new FoundMedia([TmdbId.From("27205", MediaType.Movie), TvdbId.From("81189", MediaType.Show)], new MovieDetails());

		await Assert.That(found.Fits(MediaType.Movie)).IsFalse();
	}
}
