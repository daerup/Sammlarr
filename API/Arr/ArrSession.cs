using System.Text.Json.Nodes;
using API.Arr.Types;
using Domain.Media;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal sealed class ArrSession(ArrApi api, ArrConnection connection, List<ArrTitle> titles, Dictionary<string, int> tags)
{
	public ArrApp App => api.App;

	public IReadOnlyList<ArrTitle> Titles => titles;

	public static async Task<Result<ArrSession>> OpenAsync(ArrApi api, Result<ArrConnection> connection, CancellationToken cancellationToken)
	{
		return await connection.Match(open => OpenAsync(api, open, cancellationToken), error => Task.FromResult<Result<ArrSession>>(error));
	}

	public static Result<ArrConnection> Connect(ArrApp app, ArrInstanceConfig settings)
	{
		var invalid = Error.From($"{app.Name}'s address has to start with http:// or https://.");
		return settings
			.Url.Map(url => settings.ApiKey.Map(key => (Url: url, Key: key)))
			.Match(
				given => Address(given.Url).Map(uri => new ArrConnection(app, uri, given.Key)).ToResult(invalid),
				() => Error.From($"{app.Name} is not set up: it needs an address and an API key.")
			);
	}

	private static Optional<Uri> Address(string url)
	{
		return Optional.When(Uri.TryCreate($"{url.TrimEnd('/')}/", UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", () => uri!);
	}

	public ValueSet<int> Mine(ArrTagScheme scheme, ArrTagUser user)
	{
		var labels = scheme.UserTags(user).ToHashSet();
		return [.. tags.Where(tag => scheme.PerUser ? labels.Contains(tag.Key) : scheme.Owns(tag.Key)).Select(tag => tag.Value)];
	}

	public bool IsUnclaimed(ArrTitle title, ArrTagScheme scheme)
	{
		var labels = title.Tags.Select(Label).ToList();
		return labels.Any(scheme.Owns) && !labels.Any(scheme.IsUserTag);
	}

	public bool IsKept(ArrTitle title, ArrTagScheme scheme) => title.Tags.Select(Label).Contains(scheme.KeptTag);

	public bool WantedByOthers(ArrTitle title, ArrTagScheme scheme, ValueSet<int> mine)
	{
		return title.Tags.Where(tag => !mine.Contains(tag)).Any(tag => scheme.IsUserTag(Label(tag)));
	}

	public IReadOnlyList<int> Owned(ArrTitle title, ArrTagScheme scheme) => [.. title.Tags.Where(tag => scheme.Owns(Label(tag)))];

	public Optional<int> TagId(string label) => tags.GetValueOrNone(label);

	public string Label(int id) => tags.FirstOrNone(tag => tag.Value == id).Match(tag => tag.Key, () => string.Empty);

	public Optional<ArrTitle> Find(IExternalMediaId id)
	{
		return titles.FirstOrNone(title => api.AddressOf(title).ToString() == id.Value);
	}

	public async Task<Result<IReadOnlyList<int>>> EnsureTagsAsync(IReadOnlyList<string> labels, CancellationToken cancellationToken)
	{
		Result<IReadOnlyList<int>> ids = Success.Of<IReadOnlyList<int>>([]);
		foreach (var label in labels)
		{
			var id = await TagId(label).Match(known => Task.FromResult<Result<int>>(Success.Of(known)), () => CreateTagAsync(label, cancellationToken));
			ids = ids.Map(found => id.Map(next => (IReadOnlyList<int>)[.. found, next]));
		}

		return ids;
	}

	public async Task<Result<ArrTitle>> AddAsync(JsonObject title, CancellationToken cancellationToken)
	{
		var added = await api.AddAsync(connection, title, cancellationToken);
		return added.Apply(titles.Add);
	}

	public JsonObject Prepare(JsonObject lookup, ArrInstanceConfig settings, ArrAddition addition) => api.Prepare(lookup, settings, addition);

	public Task<Result<Optional<JsonObject>>> LookupAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return api.LookupAsync(connection, id, cancellationToken);
	}

	public Task<Result<int>> RetagAsync(ArrTitle title, IReadOnlyList<int> tagIds, ArrTagChange change, CancellationToken cancellationToken)
	{
		return api.RetagAsync(connection, [title.Id], tagIds, change, cancellationToken);
	}

	public async Task<Result<int>> DeleteAsync(ArrTitle title, CancellationToken cancellationToken)
	{
		var deleted = await api.DeleteAsync(connection, title.Id, cancellationToken);
		return deleted.Apply(_ => titles.Remove(title));
	}

	public Task<Result<int>> UntagAsync(IReadOnlyList<ArrTitle> staying, IReadOnlyList<int> tagIds, CancellationToken cancellationToken)
	{
		return api.RetagAsync(connection, [.. staying.Select(title => title.Id)], tagIds, ArrTagChange.Remove, cancellationToken);
	}

	public async Task<Result<int>> DeleteAsync(IReadOnlyList<ArrTitle> doomed, CancellationToken cancellationToken)
	{
		var deleted = await api.DeleteManyAsync(connection, [.. doomed.Select(title => title.Id)], cancellationToken);
		return deleted.Apply(_ => titles.RemoveAll(title => doomed.Contains(title)));
	}

	private static async Task<Result<ArrSession>> OpenAsync(ArrApi api, ArrConnection connection, CancellationToken cancellationToken)
	{
		var titles = await api.TitlesAsync(connection, cancellationToken);
		var tags = await api.TagsAsync(connection, cancellationToken);
		return titles.Map(loaded => tags.Map(labels => new ArrSession(api, connection, [.. loaded], labels.ToDictionary(tag => tag.Label, tag => tag.Id))));
	}

	private async Task<Result<int>> CreateTagAsync(string label, CancellationToken cancellationToken)
	{
		var created = await api.CreateTagAsync(connection, label, cancellationToken);
		return created.Apply(tag => tags[tag.Label] = tag.Id).Map(tag => tag.Id);
	}
}
