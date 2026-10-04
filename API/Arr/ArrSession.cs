using System.Text.Json.Nodes;
using API.Arr.Types;
using Contracts.Settings;
using Domain.Media;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal sealed class ArrSession(ArrClient client, ArrConnection connection, List<ArrTitle> titles, Dictionary<string, int> tags)
{
	public ArrApp App => client.App;

	public IReadOnlyList<ArrTitle> Titles => titles;

	public static async Task<Result<ArrSession>> OpenAsync(ArrClient client, Result<ArrConnection> connection, CancellationToken cancellationToken)
	{
		return await connection.Match(open => OpenAsync(client, open, cancellationToken), error => Task.FromResult<Result<ArrSession>>(error));
	}

	public static Result<ArrConnection> Connect(ArrApp app, ArrInstanceConfig settings)
	{
		return settings
			.Url.Map(Address)
			.Map(uri => settings.ApiKey.Map(key => new ArrConnection(app, uri, key)))
			.Match(open => (Result<ArrConnection>)Success.Of(open), () => Error.From($"{app.Name} is not set up: it needs an address and an API key."));
	}

	private static Optional<Uri> Address(string url)
	{
		return Optional.When(Uri.TryCreate($"{url.TrimEnd('/')}/", UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https", () => uri!);
	}

	public ValueSet<int> Mine(ArrTagScheme scheme, ArrTagUser user)
	{
		var label = scheme.UserTag(user);
		return [.. tags.Where(tag => scheme.PerUser ? tag.Key == label : scheme.Owns(tag.Key)).Select(tag => tag.Value)];
	}

	public Optional<int> TagId(string label) => tags.GetValueOrNone(label);

	public string Label(int id) => tags.FirstOrNone(tag => tag.Value == id).Match(tag => tag.Key, () => string.Empty);

	public Optional<ArrTitle> Find(IExternalMediaId id)
	{
		return titles.FirstOrNone(title => (App is ArrApp.Radarr ? title.TmdbId : title.TvdbId).ToString() == id.Value);
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
		var added = await client.AddAsync(connection, title, cancellationToken);
		return added.Apply(titles.Add);
	}

	public Task<Result<Optional<JsonObject>>> LookupAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return client.LookupAsync(connection, id.Value, cancellationToken);
	}

	public Task<Result<int>> RetagAsync(ArrTitle title, IReadOnlyList<int> tagIds, ArrTagChange change, CancellationToken cancellationToken)
	{
		return client.RetagAsync(connection, title.Id, tagIds, change, cancellationToken);
	}

	public async Task<Result<int>> DeleteAsync(ArrTitle title, CancellationToken cancellationToken)
	{
		var deleted = await client.DeleteAsync(connection, title.Id, cancellationToken);
		return deleted.Apply(_ => titles.Remove(title));
	}

	private static async Task<Result<ArrSession>> OpenAsync(ArrClient client, ArrConnection connection, CancellationToken cancellationToken)
	{
		var titles = await client.TitlesAsync(connection, cancellationToken);
		var tags = await client.TagsAsync(connection, cancellationToken);
		return titles.Map(loaded => tags.Map(labels => new ArrSession(client, connection, [.. loaded], labels.ToDictionary(tag => tag.Label, tag => tag.Id))));
	}

	private async Task<Result<int>> CreateTagAsync(string label, CancellationToken cancellationToken)
	{
		var created = await client.CreateTagAsync(connection, label, cancellationToken);
		return created.Apply(tag => tags[tag.Label] = tag.Id).Map(tag => tag.Id);
	}
}
