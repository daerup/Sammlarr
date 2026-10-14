using API.Plex.Types;
using Bogus;
using Domain.Media;

namespace API.Tests.Plex.Types;

public class PlexTypesTests
{
	private static readonly Faker _faker = new();

	private static PlexTypes.ById.Metadata CreateMetadata(params PlexTypes.ById.Guid[] guids) =>
		new(
			_faker.Internet.Url(),
			_faker.Random.Number(1, 999_999).ToString(),
			_faker.Lorem.Sentence(8),
			_faker.Lorem.Sentence(4),
			"movie",
			_faker.Date.RecentOffset().ToUnixTimeSeconds(),
			_faker.Random.Double(1, 10),
			_faker.Date.Past().ToString("yyyy-MM-dd"),
			_faker.Internet.Url(),
			_faker.Lorem.Sentence(3),
			_faker.Random.Number(1980, 2025),
			_faker.Random.Number(60, 240) * 60_000,
			null,
			null,
			guids,
			[new PlexTypes.ById.Genre(_faker.Lorem.Word())]
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
}
