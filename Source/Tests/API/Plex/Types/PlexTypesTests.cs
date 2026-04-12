using API.Plex.Types;
using Bogus;
using Contracts.Media;

namespace API.Tests.Plex.Types;

public class PlexTypesTests
{
	private static readonly Faker _faker = new();

	private static PlexTypes.ById.Metadata CreateMetadata(params PlexTypes.ById.Guid[] guids) =>
		new(
			Art: _faker.Internet.Url(),
			RatingKey: _faker.Random.Number(1, 999_999).ToString(),
			Summary: _faker.Lorem.Sentence(8),
			Tagline: _faker.Lorem.Sentence(4),
			Type: "movie",
			AddedAt: _faker.Date.RecentOffset().ToUnixTimeSeconds(),
			AudienceRating: _faker.Random.Double(1, 10),
			OriginallyAvailableAt: _faker.Date.Past().ToString("yyyy-MM-dd"),
			Poster: _faker.Internet.Url(),
			Title: _faker.Lorem.Sentence(3),
			Year: _faker.Random.Number(1980, 2025),
			Duration: _faker.Random.Number(60, 240) * 60_000,
			SeasonCount: null,
			EpisodeCount: null,
			Guid: guids,
			Genre: [new PlexTypes.ById.Genre(_faker.Lorem.Word())]
		);

	[Test]
	public async Task ByIdMetadata_WhenGuidsContainKnownPrefixes_ParsesExternalIds()
	{
		var tmdbId = _faker.Random.Number(1, 999_999).ToString();
		var imdbId = $"tt{_faker.Random.Number(1_000_000, 9_999_999)}";
		var tvdbId = _faker.Random.Number(1, 999_999).ToString();

		var testee = CreateMetadata(
			new PlexTypes.ById.Guid($"tmdb://{tmdbId}"),
			new PlexTypes.ById.Guid($"imdb://{imdbId}"),
			new PlexTypes.ById.Guid($"tvdb://{tvdbId}"),
			new PlexTypes.ById.Guid($"other://{_faker.Lorem.Word()}")
		);

		await Assert.That(testee.PlexId).IsEqualTo(PlexId.From(testee.RatingKey, MediaType.Movie));
		await Assert.That(testee.TmdbIds).Count().IsEqualTo(1);
		await Assert.That(testee.TmdbIds.Single()).IsEqualTo(TmdbId.From(tmdbId, MediaType.Movie));
		await Assert.That(testee.ImdbIds).Count().IsEqualTo(1);
		await Assert.That(testee.ImdbIds.Single()).IsEqualTo(ImdbId.From(imdbId, MediaType.Movie));
		await Assert.That(testee.TvdbIds).Count().IsEqualTo(1);
		await Assert.That(testee.TvdbIds.Single()).IsEqualTo(TvdbId.From(tvdbId, MediaType.Movie));
	}

	[Test]
	public async Task ByIdMetadata_WhenGuidsContainDuplicatePrefix_ReturnsAllMatchingIds()
	{
		var tmdbId1 = _faker.Random.Number(1, 499_999).ToString();
		var tmdbId2 = _faker.Random.Number(500_000, 999_999).ToString();

		var testee = CreateMetadata(new PlexTypes.ById.Guid($"tmdb://{tmdbId1}"), new PlexTypes.ById.Guid($"tmdb://{tmdbId2}"));

		await Assert.That(testee.TmdbIds).Count().IsEqualTo(2);
		await Assert.That(testee.TmdbIds).Contains(TmdbId.From(tmdbId1, MediaType.Movie));
		await Assert.That(testee.TmdbIds).Contains(TmdbId.From(tmdbId2, MediaType.Movie));
	}

	[Test]
	public async Task ByIdMetadata_WhenGuidIsMissing_ReturnsEmptyArray()
	{
		var testee = CreateMetadata(
			new PlexTypes.ById.Guid($"tmdb://{_faker.Random.Number(1, 999_999)}"),
			new PlexTypes.ById.Guid($"tvdb://{_faker.Random.Number(1, 999_999)}")
		);

		await Assert.That(testee.ImdbIds).IsEmpty();
	}

	[Test]
	public async Task WatchlistMetadata_PlexId_ReturnsExpectedValueObject()
	{
		var ratingKey = _faker.Random.Number(1, 999_999).ToString();
		var testee = new PlexTypes.Watchlist.Metadata(
			Art: _faker.Internet.Url(),
			Rating: 8.5,
			RatingKey: ratingKey,
			Tagline: null,
			Type: "movie",
			AddedAt: (int)_faker.Date.RecentOffset().ToUnixTimeSeconds(),
			IsContinuingSeries: false,
			OriginallyAvailableAt: "2021-01-01",
			Thumb: _faker.Internet.Url(),
			Title: _faker.Lorem.Sentence(3),
			Year: 2021
		);

		await Assert.That(testee.PlexId).IsEqualTo(PlexId.From(ratingKey, MediaType.Movie));
	}
}
