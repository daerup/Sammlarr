using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.Arr.Types;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace API.Arr;

internal sealed record ArrConnection(ArrApp App, Uri BaseUri, string ApiKey)
{
	public string Address => BaseUri.ToString().TrimEnd('/');
}

internal abstract class ArrApi(HttpClient http)
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	public abstract ArrApp App { get; }

	public abstract MediaProvider AddressedBy { get; }

	protected abstract string Titles { get; }

	protected abstract string TitleIds { get; }

	protected abstract string ExclusionFlag { get; }

	public abstract int AddressOf(ArrTitle title);

	public abstract IReadOnlyList<IExternalMediaId> IdsOf(ArrTitle title);

	protected abstract void AddOptions(JsonObject title, ArrInstanceConfig settings);

	public bool Takes(IExternalMediaId id) => id.Source == AddressedBy && id.Type == App.Type;

	public JsonObject Prepare(JsonObject lookup, ArrInstanceConfig settings, ArrAddition addition)
	{
		lookup["qualityProfileId"] = addition.QualityProfileId;
		lookup["rootFolderPath"] = addition.RootFolderPath;
		lookup["monitored"] = settings.Monitored;
		lookup["tags"] = new JsonArray([.. addition.Tags.Select(tag => JsonValue.Create(tag))]);
		AddOptions(lookup, settings);
		return lookup;
	}

	protected static string Wire(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

	public Task<Result<ArrSystemStatus>> StatusAsync(ArrConnection connection, CancellationToken cancellationToken)
	{
		return ReadAsync<ArrSystemStatus>(connection, HttpMethod.Get, "system/status", new None(), cancellationToken);
	}

	public Task<Result<IReadOnlyList<ArrProfile>>> QualityProfilesAsync(ArrConnection connection, CancellationToken cancellationToken)
	{
		return ReadAsync<IReadOnlyList<ArrProfile>>(connection, HttpMethod.Get, "qualityprofile", new None(), cancellationToken);
	}

	public Task<Result<IReadOnlyList<ArrFolder>>> RootFoldersAsync(ArrConnection connection, CancellationToken cancellationToken)
	{
		return ReadAsync<IReadOnlyList<ArrFolder>>(connection, HttpMethod.Get, "rootfolder", new None(), cancellationToken);
	}

	public Task<Result<IReadOnlyList<ArrTag>>> TagsAsync(ArrConnection connection, CancellationToken cancellationToken)
	{
		return ReadAsync<IReadOnlyList<ArrTag>>(connection, HttpMethod.Get, "tag", new None(), cancellationToken);
	}

	public Task<Result<ArrTag>> CreateTagAsync(ArrConnection connection, string label, CancellationToken cancellationToken)
	{
		return ReadAsync<ArrTag>(connection, HttpMethod.Post, "tag", Some.Of<object>(new { label }), cancellationToken);
	}

	public Task<Result<IReadOnlyList<ArrTitle>>> TitlesAsync(ArrConnection connection, CancellationToken cancellationToken)
	{
		return ReadAsync<IReadOnlyList<ArrTitle>>(connection, HttpMethod.Get, Titles, new None(), cancellationToken);
	}

	public async Task<Result<Optional<JsonObject>>> LookupAsync(ArrConnection connection, IExternalMediaId id, CancellationToken cancellationToken)
	{
		var found = await ReadAsync<List<JsonObject>>(connection, HttpMethod.Get, $"{Titles}/lookup?term={ArrSearchTerm.Of(id)}", new None(), cancellationToken);
		return found.Map(titles => titles.FirstOrNone());
	}

	public Task<Result<IReadOnlyList<ArrTitle>>> SearchAsync(ArrConnection connection, string term, CancellationToken cancellationToken)
	{
		return ReadAsync<IReadOnlyList<ArrTitle>>(connection, HttpMethod.Get, $"{Titles}/lookup?term={Uri.EscapeDataString(term)}", new None(), cancellationToken);
	}

	public Task<Result<ArrTitle>> AddAsync(ArrConnection connection, JsonObject title, CancellationToken cancellationToken)
	{
		return ReadAsync<ArrTitle>(connection, HttpMethod.Post, Titles, Some.Of<object>(title), cancellationToken);
	}

	public async Task<Result<int>> RetagAsync(
		ArrConnection connection,
		IReadOnlyList<int> titleIds,
		IReadOnlyList<int> tags,
		ArrTagChange change,
		CancellationToken cancellationToken
	)
	{
		var body = new JsonObject
		{
			[TitleIds] = new JsonArray([.. titleIds.Select(id => JsonValue.Create(id))]),
			["tags"] = new JsonArray([.. tags.Select(tag => JsonValue.Create(tag))]),
			["applyTags"] = change.ToString().ToLowerInvariant(),
		};
		var sent = await SendAsync(connection, HttpMethod.Put, $"{Titles}/editor", Some.Of<object>(body), cancellationToken);
		return sent.Map(_ => titleIds.Count);
	}

	public async Task<Result<int>> DeleteAsync(ArrConnection connection, int titleId, CancellationToken cancellationToken)
	{
		var sent = await SendAsync(connection, HttpMethod.Delete, $"{Titles}/{titleId}?deleteFiles=false&{ExclusionFlag}=false", new None(), cancellationToken);
		return sent.Map(_ => titleId);
	}

	public async Task<Result<int>> DeleteManyAsync(ArrConnection connection, IReadOnlyList<int> titleIds, CancellationToken cancellationToken)
	{
		var body = new JsonObject
		{
			[TitleIds] = new JsonArray([.. titleIds.Select(id => JsonValue.Create(id))]),
			["deleteFiles"] = false,
			[ExclusionFlag] = false,
		};
		var sent = await SendAsync(connection, HttpMethod.Delete, $"{Titles}/editor", Some.Of<object>(body), cancellationToken);
		return sent.Map(_ => titleIds.Count);
	}

	private async Task<Result<T>> ReadAsync<T>(
		ArrConnection connection,
		HttpMethod method,
		string path,
		Optional<object> body,
		CancellationToken cancellationToken
	)
		where T : notnull
	{
		var sent = await SendAsync(connection, method, path, body, cancellationToken);
		return sent.Map(text => Parse<T>(connection, text));
	}

	private Result<T> Parse<T>(ArrConnection connection, string text)
		where T : notnull
	{
		try
		{
			var value = JsonSerializer.Deserialize<T>(text, Json);
			return value is not null ? Success.Of(value) : Error.From($"{App.Name} at {connection.Address} answered with nothing.");
		}
		catch (JsonException ex)
		{
			return Error.From($"{App.Name} at {connection.Address} answered something Sammlarr cannot read: {ex.Message}");
		}
	}

	private async Task<Result<string>> SendAsync(
		ArrConnection connection,
		HttpMethod method,
		string path,
		Optional<object> body,
		CancellationToken cancellationToken
	)
	{
		try
		{
			using var request = new HttpRequestMessage(method, new Uri(connection.BaseUri, $"api/v3/{path}"));
			request.Headers.Add("X-Api-Key", connection.ApiKey);
			body.Apply(content => request.Content = JsonContent.Create(content, options: Json));
			using var response = await http.SendAsync(request, cancellationToken);
			var text = await response.Content.ReadAsStringAsync(cancellationToken);
			return response.IsSuccessStatusCode ? Success.Of(text) : Error.From(Refusal(connection, response.StatusCode, text));
		}
		catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException)
		{
			return Error.From($"{App.Name} could not be reached at {connection.Address}: {ex.Message}");
		}
	}

	private string Refusal(ArrConnection connection, HttpStatusCode status, string body)
	{
		var answer = $"{App.Name} answered {(int)status} {status} at {connection.Address}.";
		return status switch
		{
			HttpStatusCode.Unauthorized => $"{answer} Check the API key under Settings › General in {App.Name}.",
			HttpStatusCode.BadRequest => $"{answer} {Validation(body)}".TrimEnd(),
			_ => answer,
		};
	}

	private static string Validation(string body)
	{
		try
		{
			return string.Join(
				" ",
				(JsonSerializer.Deserialize<List<ArrValidationFailure>>(body, Json) ?? []).Select(failure => failure.ErrorMessage).OfType<string>()
			);
		}
		catch (JsonException)
		{
			return string.Empty;
		}
	}
}

internal enum ArrTagChange
{
	Add,
	Remove,
}

internal sealed record ArrAddition(int QualityProfileId, string RootFolderPath, IReadOnlyList<int> Tags);
