using System.Net;
using System.Text.Json;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace API.Tests.Plex;

public class PlexSinkTests
{
	private const string PlexBaseUrl = "https://plex.tv";
	private static readonly Faker Faker = new();

	[Test]
	public async Task GetWantlistAsync_WhenItemHasNoYearOrDate_StillReturnsItWithoutAReleaseDate()
	{
		var json = BuildWantlistJson(1, CreateMetadata(year: 0, originallyAvailableAt: string.Empty));
		var testee = CreateWantlistService(json);

		var result = await testee.GetWantlistAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].ReleaseDate).IsNull();
	}

	[Test]
	public async Task GetWantlistAsync_WhenItemHasOnlyAYear_DatesItToThatYear()
	{
		var json = BuildWantlistJson(1, CreateMetadata(year: 1999, originallyAvailableAt: string.Empty));
		var testee = CreateWantlistService(json);

		var result = await testee.GetWantlistAsync().ToListAsync();

		await Assert.That(result[0].ReleaseDate).IsEqualTo(new DateOnly(1999, 1, 1));
	}

	[Test]
	public async Task GetWantlistAsync_WhenItemHasNoTitle_StillReturnsItWithItsExternalId()
	{
		var json = BuildWantlistJson(1, CreateMetadata(title: string.Empty, ratingKey: "plex-7"));
		var testee = CreateWantlistService(json);

		var result = await testee.GetWantlistAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].Title).IsNull();
		await Assert.That(result[0].Externals.First().Value).IsEqualTo("plex-7");
	}

	[Test]
	public async Task GetWantlistAsync_WhenItemIsComplete_CarriesItsTitleAlongsideItsExternalId()
	{
		var json = BuildWantlistJson(1, CreateMetadata(ratingKey: "plex-42", title: "Futurama"));
		var testee = CreateWantlistService(json);

		var result = await testee.GetWantlistAsync().ToListAsync();

		await Assert.That(result).Count().IsEqualTo(1);
		await Assert.That(result[0].Title).IsEqualTo("Futurama");
		await Assert.That(result[0].Externals.First().Value).IsEqualTo("plex-42");
		await Assert.That(result[0].Type).IsEqualTo(MediaType.Movie);
	}

	[Test]
	public async Task GetWantlistAsync_WhenTheWantlistIsEmpty_ReturnsNothingInsteadOfThrowing()
	{
		var testee = CreateWantlistService(EmptyWantlistJson());

		var result = await testee.GetWantlistAsync().ToListAsync();

		await Assert.That(result).IsEmpty();
	}

	[Test]
	public async Task AddToWantlistAsync_WhenRequestSucceeds_YieldsSuccessResult()
	{
		var testee = CreateWantlistService(HttpStatusCode.OK);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.AddToWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsTrue();
	}

	[Test]
	public async Task AddToWantlistAsync_WhenRequestFails_YieldsFailureResult()
	{
		var testee = CreateWantlistService(HttpStatusCode.Unauthorized);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.AddToWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsFalse();
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenRequestSucceeds_YieldsSuccessResult()
	{
		var testee = CreateWantlistService(HttpStatusCode.OK);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.RemoveFromWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsTrue();
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenRequestFails_YieldsFailureResult()
	{
		var testee = CreateWantlistService(HttpStatusCode.Unauthorized);
		var id = PlexId.From("plex-1", MediaType.Movie);

		var results = await testee.RemoveFromWantlistAsync([id]).ToListAsync();

		await Assert.That(results).Count().IsEqualTo(1);
		await Assert.That(results[0].Success).IsFalse();
	}

	private static PlexSink CreateWantlistService(string json) =>
		new(CreatePlexClient(json), PlexTestUrls.Instance, NullLogger<PlexSink>.Instance);

	private static PlexSink CreateWantlistService(HttpStatusCode statusCode) =>
		new(CreatePlexClient(statusCode), PlexTestUrls.Instance, NullLogger<PlexSink>.Instance);

	private static PlexClient CreatePlexClient(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var client = handler.CreateClient(PlexBaseUrl);
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		return new PlexClient(client, credentialStore, ICurrentUser.Mock().Object);
	}

	private static PlexClient CreatePlexClient(HttpStatusCode statusCode)
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(statusCode);
		var client = handler.CreateClient(PlexBaseUrl);
		var credential = new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialService.Mock();
		credentialStore.GetAsync(AuthProvider.Plex, Any()).Returns(credential);
		return new PlexClient(client, credentialStore, ICurrentUser.Mock().Object);
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
