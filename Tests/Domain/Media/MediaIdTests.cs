using System.Text.Json;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications;
using TestInfrastructure;
using Vogen;

namespace Domain.Tests.Media;

public class MediaIdTests
{
	private static readonly MediaId Uninitialized = new MediaId[1].Single();

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
		await Assert.That(() => MediaId.From(Guid.Empty)).Throws<ValueObjectValidationException>();
	}

	[Test]
	public async Task From_WhenValidGuid_KeepsValue()
	{
		var guid = Guid.NewGuid();

		var id = MediaId.From(guid);

		await Assert.That(id.Value).IsEqualTo(guid);
	}

	[Test]
	public async Task MediaIdentity_WhenTheMediaIdIsUninitialized_Throws()
	{
		await Assert.That(() => new MediaIdentity(Uninitialized, [TmdbId.From("550", MediaType.Movie)])).Throws<ArgumentException>();
	}

	[Test]
	public async Task ToString_WhenUninitialized_SaysSoInsteadOfThrowing()
	{
		await Assert.That(Uninitialized.ToString()).IsEqualTo("[UNINITIALIZED]");
	}

	[Test]
	public async Task Type_ForAShowIdentity_IsShow()
	{
		var identity = new MediaIdentity(MediaId.New(), [TmdbId.From("1396", MediaType.Show)]);

		await Assert.That(identity.Type).IsEqualTo(MediaType.Show);
	}

	[Test]
	public async Task Type_WithoutExternalIds_IsUnknown()
	{
		var identity = new MediaIdentity(MediaId.New(), []);

		await Assert.That(identity.Type).IsEqualTo(MediaType.Unknown);
	}

	[Test]
	public async Task ToString_IsTheGuidWithoutDashes()
	{
		var guid = new Guid("0c1d2e3f-4a5b-6c7d-8e9f-a0b1c2d3e4f5");

		await Assert.That(MediaId.From(guid).ToString()).IsEqualTo("0c1d2e3f4a5b6c7d8e9fa0b1c2d3e4f5");
	}

	[Test]
	public async Task Json_WhenANotificationCarriesMediaIds_RoundTripsThem()
	{
		var notification = new WantlistSyncCompletedNotification(UserId.New(), DateTimeOffset.UnixEpoch, [MediaId.New(), MediaId.New()], [], [], [], []);

		var roundTripped = JsonSerializer.Deserialize<WantlistSyncCompletedNotification>(JsonSerializer.Serialize(notification))!;

		await Assert.That(roundTripped.PulledAdds).IsEquivalentTo(notification.PulledAdds);
	}

	[Test]
	public async Task Equality_WhenSameGuid_AreEqual()
	{
		var guid = Guid.NewGuid();

		await Assert.That(MediaId.From(guid)).IsEqualTo(MediaId.From(guid));
	}
}
