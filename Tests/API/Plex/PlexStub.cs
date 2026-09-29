using System.Net;
using System.Text;
using System.Text.Json;
using API.Plex;
using API.Plex.Types;
using API.Tmdb;
using Bogus;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Options;
using TestInfrastructure;

namespace API.Tests.Plex;

internal sealed class PlexStub : HttpMessageHandler
{
	private const string PlexBaseUrl = "https://plex.tv";
	private const string SearchPath = "/library/search";
	private const string MetadataPath = "/library/metadata/";

	private readonly Lock _gate = new();
	private readonly List<string> _paths = [];
	private readonly Dictionary<string, PlexCandidate> _candidates = [];
	private readonly List<string> _searchOrder = [];
	private readonly Dictionary<string, HttpStatusCode> _failures = [];

	public TaskCompletionSource? MetadataGate { get; set; }

	public IReadOnlyList<string> Paths
	{
		get
		{
			lock (_gate)
			{
				return [.. _paths];
			}
		}
	}

	public int MetadataLookups(string ratingKey) => Paths.Count(path => path == MetadataPath + ratingKey);

	public int MetadataLookups() => Paths.Count(path => path.StartsWith(MetadataPath, StringComparison.Ordinal));

	public PlexStub Returns(PlexCandidate candidate)
	{
		_candidates[candidate.PlexId.Value] = candidate;
		_searchOrder.Add(candidate.PlexId.Value);
		return this;
	}

	public PlexStub RateLimits(PlexCandidate candidate) => Fails(candidate, HttpStatusCode.TooManyRequests);

	public PlexStub Fails(PlexCandidate candidate, HttpStatusCode status)
	{
		_failures[candidate.PlexId.Value] = status;
		return this;
	}

	public static TmdbApi UnmatchedTmdbApi()
	{
		var handler = Mock.HttpHandler();
		handler.OnAnyRequest().Respond(HttpStatusCode.NotFound);
		var apiKeys = IApiKeyService.Mock();
		apiKeys.GetApiKeyAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of<IApiKey>(new TestApiKey(ApiKeyProvider.Tmdb, "key")));
		var options = new TmdbOptions { BaseUrl = "https://api.themoviedb.org/3", ImageBaseUrl = "https://image.tmdb.org/t/p/original" };
		return new TmdbApi(
			new TmdbClient(handler.CreateClient(options.BaseUrl), options, apiKeys.Object, ICurrentUser.Mock().Object),
			apiKeys.Object,
			NullLogger<TmdbApi>.Instance
		);
	}

	public PlexApi CreateApi()
	{
		var client = new HttpClient(this) { BaseAddress = new Uri(PlexBaseUrl) };
		var credentials = ICredentialService.Mock();
		credentials.GetAsync(AuthProvider.Plex, Any()).Returns(new TestCredential(AuthProvider.Plex, Guid.NewGuid(), "token", DateTimeOffset.MaxValue));
		return new PlexApi(new PlexClient(client, credentials, ICurrentUser.Mock().Object), PlexTestUrls.Instance, NullLogger<PlexApi>.Instance);
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var path = request.RequestUri!.AbsolutePath;
		lock (_gate)
		{
			_paths.Add(path);
		}

		if (path.StartsWith(SearchPath, StringComparison.Ordinal))
		{
			return Json(BuildSearchPayload());
		}

		if (MetadataGate is { } gate)
		{
			await gate.Task.WaitAsync(cancellationToken);
		}

		var ratingKey = path[MetadataPath.Length..];
		if (_failures.TryGetValue(ratingKey, out var status))
		{
			return new HttpResponseMessage(status);
		}

		return Json(BuildMetadataPayload(_candidates[ratingKey]));
	}

	private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

	private string BuildSearchPayload()
	{
		var metadata = _searchOrder
			.Select(key => _candidates[key])
			.Select(candidate => new PlexTypes.Search.Metadata(
				candidate.PlexId.Value,
				candidate.Title,
				null,
				null,
				"movie",
				candidate.Year,
				null,
				null,
				null,
				null,
				null
			))
			.Select(meta => new PlexTypes.Search.NestedSearchResult(meta))
			.ToList();
		var container = new PlexTypes.Search.MediaContainer([], "search", 1, [new PlexTypes.Search.SearchResult(metadata.Count, metadata)]);
		return JsonSerializer.Serialize(new PlexTypes.Search.Root(container));
	}

	private static string BuildMetadataPayload(PlexCandidate candidate)
	{
		var faker = new Faker();
		var metadata = new PlexTypes.ById.Metadata(
			faker.Internet.Url(),
			candidate.PlexId.Value,
			faker.Lorem.Paragraph(),
			faker.Lorem.Sentence(3),
			"movie",
			faker.Date.PastOffset().ToUnixTimeSeconds(),
			faker.Random.Double(1, 10),
			faker.Date.PastDateOnly().ToString("yyyy-MM-dd"),
			faker.Internet.Url(),
			faker.Lorem.Sentence(2),
			faker.Date.Past().Year,
			faker.Random.Int(80, 200) * 60 * 1000,
			null,
			null,
			[new PlexTypes.ById.Guid($"tmdb://{candidate.TmdbId.Value}")],
			[new PlexTypes.ById.Genre(faker.Lorem.Word())]
		);
		var container = new PlexTypes.ById.MediaContainer("1", "Movies", 0, 1, [metadata]);
		return JsonSerializer.Serialize(new PlexTypes.ById.Root(container));
	}
}

internal sealed record PlexCandidate(PlexId PlexId, TmdbId TmdbId, string Title = "match", int Year = 2021)
{
	public static PlexCandidate Create(string title = "match", int year = 2021)
	{
		var faker = new Faker();
		return new PlexCandidate(
			PlexId.From($"plex-{faker.Random.AlphaNumeric(10)}", MediaType.Movie),
			TmdbId.From(faker.Random.Int(1, 1_000_000).ToString(), MediaType.Movie),
			title,
			year
		);
	}
}
