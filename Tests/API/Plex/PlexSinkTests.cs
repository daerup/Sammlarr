using System.Net;
using System.Text.Json;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Plex;

public class PlexSinkTests
{
	private const string PlexBaseUrl = "https://plex.tv";
	private static readonly Faker Faker = new();
	private static readonly MediaId Known = MediaId.New();

	[Test]
	[Arguments(MediaType.Movie)]
	[Arguments(MediaType.Show)]
	[Arguments(MediaType.Unknown)]
	public async Task AddressedBy_ForEveryMediaType_IsPlex(MediaType type) =>
		await Assert.That(Sink(HttpStatusCode.OK, IMediaService.Mock()).AddressedBy(type)).IsSome().And.IsEqualTo(MediaProvider.Plex);

	[Test]
	public async Task GetAsync_WhenAnItemMatches_ReturnsItsMediaIdFromItsPlexId()
	{
		var media = MatchingAs(Known);
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42")), media);

		var snapshot = await testee.GetAsync();

		await Assert.That(snapshot.Ids).IsEquivalentTo([Known]);
		await Assert.That(snapshot.Unmatched).IsEmpty();
		media
			.IdentifyAsync(Is<IEnumerable<IdentifyRequest>>(requests => requests?.Single() is { Candidates: [{ Value: "plex-42" }] }), Any())
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenAnItemCannotBeMatched_ReportsItWithItsTitleAndPlexId()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42", title: "Futurama")), MatchingNothing());

		var snapshot = await testee.GetAsync();

		await Assert.That(snapshot.Ids).IsEmpty();
		await Assert.That(snapshot.Unmatched.Single().Details.Title).IsEqualTo("Futurama");
		await Assert.That(snapshot.Unmatched.Single().Externals.Single().Value).IsEqualTo("plex-42");
		await Assert.That(snapshot.Unmatched.Single().Details.Type).IsEqualTo(MediaType.Movie);
	}

	[Test]
	public async Task GetAsync_WhenAnItemCannotBeMatched_WarnsWithItsTitle()
	{
		var logger = ILogger<PlexSink>.Mock();
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42", title: "Futurama")), MatchingNothing(), logger.Object);

		await testee.GetAsync();

		await Assert
			.That(Mock.Invocations(logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}"))
			.IsEquivalentTo(["Plex watchlist item Futurama could not be matched during sync"]);
	}

	[Test]
	public async Task GetAsync_WhenAnUnmatchedItemHasNoTitle_StillReportsItsPlexId()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(title: string.Empty, ratingKey: "plex-7")), MatchingNothing());

		var unmatched = (await testee.GetAsync()).Unmatched.Single();

		await Assert.That(unmatched.Details.Title).IsNull();
		await Assert.That(unmatched.Externals.Single().Value).IsEqualTo("plex-7");
	}

	[Test]
	public async Task GetAsync_WhenAnUnmatchedItemHasNoYearOrDate_ReportsItWithoutAReleaseDate()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(year: 0, originallyAvailableAt: string.Empty)), MatchingNothing());

		var unmatched = (await testee.GetAsync()).Unmatched.Single();

		await Assert.That(unmatched.Details.ReleaseDate).IsNull();
	}

	[Test]
	public async Task GetAsync_WhenAnUnmatchedItemHasOnlyAYear_DatesItToThatYear()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(year: 1999, originallyAvailableAt: string.Empty)), MatchingNothing());

		var unmatched = (await testee.GetAsync()).Unmatched.Single();

		await Assert.That(unmatched.Details.ReleaseDate).IsEqualTo(new DateOnly(1999, 1, 1));
	}

	[Test]
	public async Task GetAsync_WhenTheWatchlistIsEmpty_ReturnsNothingInsteadOfThrowing()
	{
		var testee = Sink(EmptyWantlistJson(), MatchingNothing());

		var snapshot = await testee.GetAsync();

		await Assert.That(snapshot.Ids).IsEmpty();
		await Assert.That(snapshot.Unmatched).IsEmpty();
	}

	[Test]
	[Arguments(WantlistOperationAction.Add, HttpStatusCode.OK, SinkSyncOutcome.Applied)]
	[Arguments(WantlistOperationAction.Add, HttpStatusCode.Unauthorized, SinkSyncOutcome.Failed)]
	[Arguments(WantlistOperationAction.Remove, HttpStatusCode.OK, SinkSyncOutcome.Applied)]
	[Arguments(WantlistOperationAction.Remove, HttpStatusCode.Unauthorized, SinkSyncOutcome.Failed)]
	public async Task ApplyAsync_ForATitleWithAPlexId_AcksWhatPlexAnswered(WantlistOperationAction action, HttpStatusCode status, SinkSyncOutcome expected)
	{
		var testee = Sink(status, Addressing(Known, PlexId.From("plex-1", MediaType.Movie)));

		var acks = await testee.ApplyAsync(action, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks.Select(ack => (ack.MediaId, ack.Outcome))).IsEquivalentTo([(Known, expected)]);
	}

	[Test]
	public async Task ApplyAsync_WhenPlexRefuses_AcksItFailedWithPlexsAnswer()
	{
		var testee = Sink(HttpStatusCode.Unauthorized, Addressing(Known, PlexId.From("plex-1", MediaType.Movie)));

		var acks = await testee.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert.That(acks).IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed) { Reason = Some.Of("Plex answered 401 Unauthorized.") }]);
	}

	[Test]
	public async Task ApplyAsync_WhenPlexCannotBeReached_AcksItFailedSayingWhy()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Throws(new HttpRequestException("No such host is known."));
		var testee = Sink(handler, Addressing(Known, PlexId.From("plex-1", MediaType.Movie)));

		var acks = await testee.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Failed) { Reason = Some.Of("Plex could not be reached: No such host is known.") }]);
	}

	[Test]
	public async Task ApplyAsync_ForATitleWithoutAPlexId_AcksUnaddressableWithoutCallingPlex()
	{
		var handler = Mock.HttpHandler();
		var media = IMediaService.Mock();
		media.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, IExternalMediaId>());
		var testee = Sink(handler, media);

		var acks = await testee.ApplyAsync(WantlistOperationAction.Add, [new SinkPush(Known, new None())]).ToListAsync();

		await Assert
			.That(acks)
			.IsEquivalentTo([new SinkSyncAck(Known, SinkSyncOutcome.Unaddressable) { Reason = Some.Of("Sammlarr found no Plex id for this title.") }]);
		await Assert.That(handler.Requests).IsEmpty();
	}

	private static PlexSink Sink(string json, Mock<IMediaService> media, ILogger<PlexSink>? logger = null)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		return Sink(handler, media, logger);
	}

	private static PlexSink Sink(HttpStatusCode status, Mock<IMediaService> media) => Sink(Mock.HttpHandler().WithDefaultStatus(status), media);

	private static PlexSink Sink(MockHttpHandler handler, Mock<IMediaService> media, ILogger<PlexSink>? logger = null)
	{
		return new PlexSink(Client(handler), PlexTestUrls.Instance, media.Object, logger ?? NullLogger<PlexSink>.Instance);
	}

	private static Mock<IMediaService> MatchingAs(MediaId mediaId)
	{
		return Identifying(request => Success.Of(new MediaIdentity(mediaId, [.. request.Candidates.OfType<IIdentityKey>()])));
	}

	private static Mock<IMediaService> MatchingNothing() => Identifying(_ => Error.From("unmatched"));

	private static Mock<IMediaService> Identifying(Func<IdentifyRequest, Result<MediaIdentity>> identify)
	{
		var media = IMediaService.Mock();
		media.IdentifyAsync(Any<IEnumerable<IdentifyRequest>>(), Any()).Returns((requests, _) => requests.Select(identify).ToAsyncEnumerable());
		return media;
	}

	private static Mock<IMediaService> Addressing(MediaId mediaId, IExternalMediaId plexId)
	{
		var media = IMediaService.Mock();
		media.GetSinkIdsAsync(Any<IEnumerable<MediaId>>(), Any()).Returns(new Dictionary<MediaId, IExternalMediaId> { [mediaId] = plexId });
		return media;
	}

	private static PlexClient Client(MockHttpHandler handler)
	{
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		return new PlexClient(handler.CreateClient(PlexBaseUrl), credentialStore, ICurrentUser.Mock().Object);
	}

	private static string BuildWantlistJson(int totalSize, object metadata)
	{
		var payload = new
		{
			MediaContainer = new
			{
				identifier = "plex",
				offset = 0,
				totalSize,
				size = 1,
				Metadata = new[] { metadata },
			},
		};

		return JsonSerializer.Serialize(payload);
	}

	private static string EmptyWantlistJson() =>
		JsonSerializer.Serialize(
			new
			{
				MediaContainer = new
				{
					identifier = "plex",
					offset = 0,
					totalSize = 0,
					size = 0,
				},
			}
		);

	private static object CreateMetadata(int year = 2021, string? originallyAvailableAt = "2021-01-01", string title = "Movie 1", string? ratingKey = null) =>
		new
		{
			art = $"/art/{Faker.Random.Int(1, 1000)}",
			rating = Faker.Random.Double(1, 10),
			ratingKey = ratingKey ?? $"plex-{Faker.Random.Int(1, 1000)}",
			tagline = Faker.Lorem.Sentence(3),
			type = "movie",
			addedAt = (int)Faker.Date.RecentOffset().ToUnixTimeSeconds(),
			isContinuingSeries = false,
			originallyAvailableAt,
			thumb = $"/thumb/{Faker.Random.Int(1, 1000)}",
			title,
			year,
		};
}
