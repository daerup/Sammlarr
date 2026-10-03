using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class MediaIdTests
{
	[Test]
	public async Task Require_WhenMatchingType_IsSuccess()
	{
		IExternalMediaId id = TmdbId.From("550", MediaType.Movie);

		await Assert.That(id.Require<TmdbId>("wrong type")).IsSuccess().And.Multiple(async tmdbId => await Assert.That(tmdbId.Value).IsEqualTo("550"));
	}

	[Test]
	public async Task Require_WhenDifferentType_IsErrorWithReason()
	{
		IExternalMediaId id = PlexId.From("plex-1", MediaType.Movie);

		var result = id.Require<TmdbId>("needs a TMDB id");

		await Assert.That(result.Match(_ => "ok", error => error.Reason)).IsEqualTo("needs a TMDB id");
	}

	[Test]
	public async Task From_WhenEmptyGuid_Throws()
	{
		await Assert.That(() => MediaId.From(Guid.Empty)).Throws<ArgumentException>();
	}

	[Test]
	public async Task From_WhenValidGuid_KeepsValue()
	{
		var guid = Guid.NewGuid();

		var id = MediaId.From(guid);

		await Assert.That(id.Value).IsEqualTo(guid);
	}

	[Test]
	public async Task MediaIdentity_WhenTheMediaIdIsDefault_Throws()
	{
		await Assert.That(() => new MediaIdentity(new MediaId(), [TmdbId.From("550", MediaType.Movie)])).Throws<ArgumentException>();
	}

	[Test]
	public async Task Type_ForAShowIdentity_IsShow()
	{
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("1396", MediaType.Show)]);

		await Assert.That(identity.Type).IsEqualTo(MediaType.Show);
	}

	[Test]
	public async Task Type_WithoutExternalIds_IsUnknown()
	{
		var identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), []);

		await Assert.That(identity.Type).IsEqualTo(MediaType.Unknown);
	}

	[Test]
	public async Task Equality_WhenSameGuid_AreEqual()
	{
		var guid = Guid.NewGuid();

		await Assert.That(MediaId.From(guid)).IsEqualTo(MediaId.From(guid));
	}
}
