using System.Net;
using System.Text.Json;
using API.Plex;
using Bogus;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.Tests.Plex;

public class PlexWatchListServiceTests
{
	private const string PlexBaseUrl = "https://plex.tv";
	private static readonly Faker Faker = new();

	[Test]
	public async Task GetWatchlistAsync_WhenItemHasZeroYear_FiltersItem()
	{
		var json = BuildWatchlistJson(0, CreateMetadata(0));
		var testee = CreateWatchListService(json);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.Items).IsEmpty();
	}

	[Test]
	public async Task GetWatchlistAsync_WhenItemHasMissingDate_FiltersItem()
	{
		var json = BuildWatchlistJson(0, CreateMetadata(originallyAvailableAt: string.Empty));
		var testee = CreateWatchListService(json);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.Items).IsEmpty();
	}

	[Test]
	public async Task GetWatchlistAsync_WhenItemHasMissingTitle_FiltersItem()
	{
		var json = BuildWatchlistJson(0, CreateMetadata(title: string.Empty));
		var testee = CreateWatchListService(json);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.Items).IsEmpty();
	}

	[Test]
	public async Task GetWatchlistAsync_WhenItemIsComplete_ReturnsItem()
	{
		var json = BuildWatchlistJson(1, CreateMetadata(title: "Movie 1"));
		var testee = CreateWatchListService(json);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.Items).Count().IsEqualTo(1);
		await Assert.That(result.Items[0].Title).IsEqualTo("Movie 1");
		await Assert.That(result.Items[0].MediaType).IsEqualTo(MediaType.Movie);
	}

	[Test]
	[Arguments(250, 3)]
	[Arguments(101, 2)]
	[Arguments(100, 1)]
	[Arguments(0, 1)]
	public async Task GetWatchlistAsync_CalculatesTotalPages(int totalSize, int expectedPages)
	{
		var json = BuildWatchlistJson(totalSize, CreateMetadata());
		var testee = CreateWatchListService(json);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.TotalPages).IsEqualTo(expectedPages);
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenRequestSucceeds_ReturnsTrue()
	{
		var testee = CreateWatchListService(HttpStatusCode.OK);

		var result = await testee.AddToWatchlistAsync(PlexId.From("plex-1", MediaType.Movie));

		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task AddToWatchlistAsync_WhenRequestFails_ReturnsFalse()
	{
		var testee = CreateWatchListService(HttpStatusCode.Unauthorized);

		var result = await testee.AddToWatchlistAsync(PlexId.From("plex-1", MediaType.Movie));

		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenRequestSucceeds_ReturnsTrue()
	{
		var testee = CreateWatchListService(HttpStatusCode.OK);

		var result = await testee.RemoveFromWatchlistAsync(PlexId.From("plex-1", MediaType.Movie));

		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_WhenRequestFails_ReturnsFalse()
	{
		var testee = CreateWatchListService(HttpStatusCode.Unauthorized);

		var result = await testee.RemoveFromWatchlistAsync(PlexId.From("plex-1", MediaType.Movie));

		await Assert.That(result).IsFalse();
	}

	private static PlexWatchListService CreateWatchListService(string json)
	{
		var resolver = IMediaIdResolver.Mock();
		resolver.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any()).Returns(new Unresolved());
		return new PlexWatchListService(CreatePlexClient(json), resolver);
	}

	private static PlexWatchListService CreateWatchListService(HttpStatusCode statusCode)
	{
		var resolver = IMediaIdResolver.Mock();
		resolver.ResolveAsync(Any<IReadOnlyCollection<IMediaId>>(), Any<MediaProvider>(), Any()).Returns(new Unresolved());
		return new PlexWatchListService(CreatePlexClient(statusCode), resolver);
	}

	private static PlexClient CreatePlexClient(string json)
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().RespondWithJson(json);
		var client = handler.CreateClient(PlexBaseUrl);
		var credential = new StoredCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		return new PlexClient(client, credentialStore);
	}

	private static PlexClient CreatePlexClient(HttpStatusCode statusCode)
	{
		var handler = Mock.HttpHandler().WithDefaultStatus(statusCode);
		var client = handler.CreateClient(PlexBaseUrl);
		var credential = new StoredCredential(WatchlistProvider.Plex, Guid.NewGuid(), "test-token", DateTimeOffset.MaxValue);
		var credentialStore = ICredentialStore.Mock();
		credentialStore.GetAsync(WatchlistProvider.Plex, Any()).Returns(credential);
		return new PlexClient(client, credentialStore);
	}

	private static string BuildWatchlistJson(int totalSize, object metadata)
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

	private static object CreateMetadata(int year = 2021, string? originallyAvailableAt = "2021-01-01", string title = "Movie 1") =>
		new
		{
			art = $"/art/{Faker.Random.Int(1, 1000)}",
			rating = Faker.Random.Double(1, 10),
			ratingKey = $"plex-{Faker.Random.Int(1, 1000)}",
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
