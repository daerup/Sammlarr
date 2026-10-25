using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.Arr.Types;
using API.Http.Lanes;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Polly.Timeout;

namespace API.Arr;

internal sealed record ArrConnection(ArrApp App, Uri BaseUri, Secret ApiKey)
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

	protected abstract string AddressField { get; }

	protected abstract string ExclusionFlag { get; }

	public abstract int AddressOf(ArrTitle title);

	public abstract IReadOnlyList<IExternalMediaId> IdsOf(ArrTitle title);

	protected abstract void AddOptions(JsonObject title, ArrInstanceConfig settings, ArrRoute route);

	public abstract bool IsAnime(JsonObject lookup);

	public bool Takes(IExternalMediaId id) => id.Source == AddressedBy && id.Type == App.Type;

	public JsonObject Body(IExternalMediaId id, string title) => new() { [AddressField] = int.Parse(id.Value, CultureInfo.InvariantCulture), ["title"] = title };

	public JsonObject Prepare(JsonObject title, ArrInstanceConfig settings, ArrAddition addition)
	{
		title["qualityProfileId"] = addition.QualityProfileId;
		title["rootFolderPath"] = addition.RootFolderPath;
		title["monitored"] = settings.Monitored;
		title["tags"] = new JsonArray([.. addition.Tags.Select(tag => JsonValue.Create(tag))]);
		AddOptions(title, settings, addition.Route);
		return title;
	}

	protected static string Wire(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

	protected static bool Lists(JsonObject lookup, string field, string label)
	{
		return (lookup[field]?.AsArray() ?? []).Any(entry => string.Equals(entry?.GetValue<string>(), label, StringComparison.OrdinalIgnoreCase));
	}

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

	public async Task<Result<Optional<ArrTitle>>> AddAsync(ArrConnection connection, JsonObject title, CancellationToken cancellationToken)
	{
		var answered = await ExchangeAsync(connection, HttpMethod.Post, Titles, Some.Of<object>(title), cancellationToken);
		return answered.Map(answer => Added(connection, answer));
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

	public async Task<Result<int>> DeleteAsync(ArrConnection connection, int titleId, ArrInstanceConfig settings, CancellationToken cancellationToken)
	{
		var path = FormattableString.Invariant($"{Titles}/{titleId}?deleteFiles={Flag(settings.DeleteFiles)}&{ExclusionFlag}=false");
		var sent = await SendAsync(connection, HttpMethod.Delete, path, new None(), cancellationToken);
		return sent.Map(_ => titleId);
	}

	public async Task<Result<int>> DeleteManyAsync(
		ArrConnection connection,
		IReadOnlyList<int> titleIds,
		ArrInstanceConfig settings,
		CancellationToken cancellationToken
	)
	{
		var body = new JsonObject
		{
			[TitleIds] = new JsonArray([.. titleIds.Select(id => JsonValue.Create(id))]),
			["deleteFiles"] = settings.DeleteFiles,
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

	private Result<Optional<ArrTitle>> Added(ArrConnection connection, ArrAnswer answer)
	{
		if (answer.Status is HttpStatusCode.BadRequest && Failures(answer.Text).Any(NamesAnUnknownId))
		{
			return Success.Of<Optional<ArrTitle>>(new None());
		}

		return Accepted(connection, answer).Map(text => Parse<ArrTitle>(connection, text)).Map(added => Optional.Of(added));
	}

	private bool NamesAnUnknownId(ArrValidationFailure failure)
	{
		return string.Equals(failure.PropertyName, AddressField, StringComparison.OrdinalIgnoreCase)
			&& failure.ErrorMessage?.Contains("was not found", StringComparison.OrdinalIgnoreCase) is true;
	}

	private async Task<Result<string>> SendAsync(
		ArrConnection connection,
		HttpMethod method,
		string path,
		Optional<object> body,
		CancellationToken cancellationToken
	)
	{
		var answered = await ExchangeAsync(connection, method, path, body, cancellationToken);
		return answered.Map(answer => Accepted(connection, answer));
	}

	private async Task<Result<ArrAnswer>> ExchangeAsync(
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
			request.Headers.Add("X-Api-Key", connection.ApiKey.Reveal());
			request.Options.Set(LaneHandler.Owner, connection.Address);
			body.Apply(content => request.Content = JsonContent.Create(content, options: Json));
			using var response = await http.SendAsync(request, cancellationToken);
			return Success.Of(new ArrAnswer(response.IsSuccessStatusCode, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken)));
		}
		catch (Exception ex) when (ex is HttpRequestException or LaneRefusedException or TimeoutRejectedException)
		{
			return Error.From($"{App.Name} could not be reached at {connection.Address}: {ex.Message}");
		}
	}

	private Result<string> Accepted(ArrConnection connection, ArrAnswer answer)
	{
		return answer.Succeeded ? Success.Of(answer.Text) : Refusal(connection, answer.Status, answer.Text);
	}

	private Error Refusal(ArrConnection connection, HttpStatusCode status, string body)
	{
		var answer = $"{App.Name} answered {(int)status} {status} at {connection.Address}.";
		return status switch
		{
			HttpStatusCode.Unauthorized => KeyRefused($"{answer} Check the API key under Settings › General in {App.Name}."),
			HttpStatusCode.BadRequest => Error.From($"{answer} {Validation(body)}".TrimEnd()),
			_ => Error.From(answer),
		};
	}

	private static Error KeyRefused(string reason) => Error.From(reason, new ArrKeyRefusedException(reason));

	private static string Validation(string body) => string.Join(" ", Failures(body).Select(failure => failure.ErrorMessage).OfType<string>());

	private static IReadOnlyList<ArrValidationFailure> Failures(string body)
	{
		try
		{
			return JsonSerializer.Deserialize<List<ArrValidationFailure>>(body, Json) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static string Flag(bool value) => value ? "true" : "false";

	private sealed record ArrAnswer(bool Succeeded, HttpStatusCode Status, string Text);
}

internal enum ArrTagChange
{
	Add,
	Remove,
}

internal sealed record ArrAddition(int QualityProfileId, string RootFolderPath, IReadOnlyList<int> Tags, ArrRoute Route = ArrRoute.Default)
{
	public ArrAddition Routed(ArrRoute route, ArrInstanceConfig settings)
	{
		return this with
		{
			QualityProfileId = settings.ProfileFor(route).GetValueOr(QualityProfileId),
			RootFolderPath = settings.FolderFor(route).GetValueOr(RootFolderPath),
			Route = route,
		};
	}
}
