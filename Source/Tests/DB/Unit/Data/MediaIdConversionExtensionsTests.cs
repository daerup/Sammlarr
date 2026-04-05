using Contracts.Media;
using DB.Data;
using DB.Data.Entities;

namespace DB.Tests.Unit.Data;

public class MediaIdConversionExtensionsTests
{
	[Test]
	[Arguments(MediaProvider.Tmdb, "12345")]
	[Arguments(MediaProvider.Imdb, "tt1234567")]
	[Arguments(MediaProvider.Tvdb, "98765")]
	[Arguments(MediaProvider.Plex, "plex-123")]
	public async Task MapEntity_WhenMediaIdProvided_MapsSourceAndExternalId(MediaProvider source, string externalId)
	{
		var testee = CreateMediaId(source, externalId);

		var entry = testee.MapEntity();

		await Assert.That(entry.Source).IsEqualTo(source);
		await Assert.That(entry.ExternalId).IsEqualTo(externalId);
		await Assert.That(entry.MapValueObject()).IsEqualTo(testee);
	}

	[Test]
	[Arguments(MediaProvider.Tmdb, "12345")]
	[Arguments(MediaProvider.Imdb, "tt1234567")]
	[Arguments(MediaProvider.Tvdb, "98765")]
	[Arguments(MediaProvider.Plex, "plex-123")]
	public async Task MapValueObject_WhenEntryProvided_MapsBackToSameValueObject(MediaProvider source, string externalId)
	{
		var testee = new MediaIdEntry { Source = source, ExternalId = externalId };
		var expected = CreateMediaId(source, externalId);

		var mediaId = testee.MapValueObject();

		await Assert.That(mediaId.Source).IsEqualTo(source);
		await Assert.That(mediaId.ToString()).IsEqualTo(externalId);
		await Assert.That(mediaId).IsEqualTo(expected);
	}

	private static IMediaId CreateMediaId(MediaProvider source, string externalId)
	{
		return source switch
		{
			MediaProvider.Tmdb => TmdbId.Parse(externalId),
			MediaProvider.Imdb => ImdbId.From(externalId),
			MediaProvider.Tvdb => TvdbId.Parse(externalId),
			MediaProvider.Plex => PlexId.From(externalId),
		};
	}
}
