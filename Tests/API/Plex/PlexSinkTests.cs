using System.Net;
using System.Text.Json;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;
using TUnit.Mocks.Http;

namespace API.Tests.Plex;

public class PlexSinkTests
{
	private const string PlexBaseUrl = "https://plex.tv";
	private static readonly Faker Faker = new();
	private static readonly MediaId Known = MediaId.From(Guid.NewGuid());

	[Test]
	public async Task ReadAsync_WhenAnItemMatches_ReturnsItsMediaIdFromItsPlexId()
	{
		var media = MatchingAs(Known);
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42")), media);

		var contents = await testee.ReadAsync();

		await Assert.That(contents.Matched).IsEquivalentTo([Known]);
		await Assert.That(contents.Unmatched).IsEmpty();
		media
			.IdentifyAsync(
				Arg.Is<IEnumerable<ResolveRequest>>(requests => requests?.Single() is { Target: MediaProvider.Plex, Candidates: [{ Value: "plex-42" }] }),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task ReadAsync_WhenAnItemCannotBeMatched_ReportsItWithItsTitleAndPlexId()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42", title: "Futurama")), MatchingNothing());

		var contents = await testee.ReadAsync();

		await Assert.That(contents.Matched).IsEmpty();
		await Assert.That(contents.Unmatched.Single().Title).IsEqualTo("Futurama");
		await Assert.That(contents.Unmatched.Single().Externals.Single().Value).IsEqualTo("plex-42");
		await Assert.That(contents.Unmatched.Single().Type).IsEqualTo(MediaType.Movie);
	}

	[Test]
	public async Task ReadAsync_WhenAnUnmatchedItemHasNoTitle_StillReportsItsPlexId()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(title: string.Empty, ratingKey: "plex-7")), MatchingNothing());

		var unmatched = (await testee.ReadAsync()).Unmatched.Single();

		await Assert.That(unmatched.Title).IsNull();
		await Assert.That(unmatched.Externals.Single().Value).IsEqualTo("plex-7");
	}

	[Test]
	public async Task ReadAsync_WhenAnUnmatchedItemHasNoYearOrDate_ReportsItWithoutAReleaseDate()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(year: 0, originallyAvailableAt: string.Empty)), MatchingNothing());

		var unmatched = (await testee.ReadAsync()).Unmatched.Single();

		await Assert.That(unmatched.ReleaseDate).IsNull();
	}

	[Test]
	public async Task ReadAsync_WhenAnUnmatchedItemHasOnlyAYear_DatesItToThatYear()
	{
		var testee = Sink(BuildWantlistJson(1, CreateMetadata(year: 1999, originallyAvailableAt: string.Empty)), MatchingNothing());

		var unmatched = (await testee.ReadAsync()).Unmatched.Single();

		await Assert.That(unmatched.ReleaseDate).IsEqualTo(new DateOnly(1999, 1, 1));
	}

	[Test]
	public async Task ReadAsync_WhenTheWatchlistIsEmpty_ReturnsNothingInsteadOfThrowing()
	{
		var testee = Sink(EmptyWantlistJson(), MatchingNothing());

		var contents = await testee.ReadAsync();

		await Assert.That(contents.Matched).IsEmpty();
		await Assert.That(contents.Unmatched).IsEmpty();
	}

	[Test]
	public async Task AddToWantlistAsync_WhenRequestSucceeds_YieldsSuccessResult()
	{
		var testee = Sink(HttpStatusCode.OK);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.AddToWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsTrue();
	}

	[Test]
	public async Task AddToWantlistAsync_WhenRequestFails_YieldsFailureResult()
	{
		var testee = Sink(HttpStatusCode.Unauthorized);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.AddToWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsFalse();
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenRequestSucceeds_YieldsSuccessResult()
	{
		var testee = Sink(HttpStatusCode.OK);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.RemoveFromWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsTrue();
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenRequestFails_YieldsFailureResult()
	{
		var testee = Sink(HttpStatusCode.Unauthorized);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.RemoveFromWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsFalse();
	}

	private static PlexSink Sink(string json, Mock<IMediaService> media)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		return Sink(handler, media);
	}

	private static PlexSink Sink(HttpStatusCode status) => Sink(Mock.HttpHandler().WithDefaultStatus(status), IMediaService.Mock());

	private static PlexSink Sink(MockHttpHandler handler, Mock<IMediaService> media) =>
		new(Client(handler), PlexTestUrls.Instance, media.Object, NullLogger<PlexSink>.Instance);

	private static Mock<IMediaService> MatchingAs(MediaId mediaId) => Identifying(request => Success.Of(new MediaIdentity(mediaId, [.. request.Candidates])));

	private static Mock<IMediaService> MatchingNothing() => Identifying(_ => Error.From("unmatched"));

	private static Mock<IMediaService> Identifying(Func<ResolveRequest, Result<MediaIdentity>> identify)
	{
		var media = IMediaService.Mock();
		media.IdentifyAsync(Any<IEnumerable<ResolveRequest>>(), Any()).Returns((requests, _) => requests.Select(identify).ToAsyncEnumerable());
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
