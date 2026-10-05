using System.Globalization;
using System.Text.Json.Nodes;

namespace E2E.Tests.SelfHosted;

internal sealed class ArrApi(HttpClient http)
{
	internal Uri Url => http.BaseAddress!;

	internal async Task<JsonNode> GetAsync(string path)
	{
		using var response = await http.GetAsync($"api/v3/{path}");
		response.EnsureSuccessStatusCode();
		return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
	}

	internal async Task<JsonNode> PostAsync(string path, object body)
	{
		using var response = await http.PostAsJsonAsync($"api/v3/{path}", body);
		response.EnsureSuccessStatusCode();
		return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
	}

	internal async Task ClearAsync(string library)
	{
		foreach (var item in (await GetAsync(library)).AsArray())
		{
			await DeleteAsync($"{library}/{item!["id"]}?deleteFiles=true");
		}

		foreach (var tag in (await GetAsync("tag")).AsArray())
		{
			await DeleteAsync($"tag/{tag!["id"]}");
		}

		foreach (var indexer in (await GetAsync("indexer")).AsArray())
		{
			await DeleteAsync($"indexer/{indexer!["id"]}");
		}
	}

	internal async Task<JsonNode> WaitForAsync(string library, Func<JsonNode, bool> match)
	{
		JsonNode? found = null;
		await Poll.UntilAsync(async () => (found = await FindAsync(library, match)) is not null, () => $"{http.BaseAddress}api/v3/{library} never held the title");
		return found!;
	}

	internal Task WaitForRefreshAsync(JsonNode movie)
	{
		return Poll.UntilAsync(
			async () => (await GetAsync("command")).AsArray().Any(command => Refreshed(command!, movie)),
			() => $"{http.BaseAddress} never refreshed movie {movie["id"]}"
		);
	}

	internal Task SettleAsync()
	{
		IReadOnlyList<string> busy = [];
		return Poll.UntilAsync(async () => (busy = await BusyAsync()).Count == 0, () => $"{http.BaseAddress} still runs {string.Join(", ", busy)}");
	}

	internal static ArrApi For(Uri url)
	{
		var http = new HttpClient { BaseAddress = url };
		http.DefaultRequestHeaders.Add("X-Api-Key", StackBoot.ArrApiKey);
		return new ArrApi(http);
	}

	private static bool Refreshed(JsonNode command, JsonNode movie)
	{
		return command["name"]!.GetValue<string>() == "RefreshMovie"
			&& command["status"]!.GetValue<string>() == "completed"
			&& command["body"]!["movieIds"]!.AsArray().Any(id => id!.GetValue<int>() == movie["id"]!.GetValue<int>())
			&& Time(command["queued"]!) >= Time(movie["added"]!);
	}

	private static DateTimeOffset Time(JsonNode value) => DateTimeOffset.Parse(value.GetValue<string>(), CultureInfo.InvariantCulture);

	private async Task<IReadOnlyList<string>> BusyAsync()
	{
		var commands = (await GetAsync("command")).AsArray();
		return
		[
			.. commands
				.Where(command => command!["status"]!.GetValue<string>() is "queued" or "started")
				.Select(command => $"{command!["name"]} ({command["status"]})"),
		];
	}

	private async Task<JsonNode?> FindAsync(string library, Func<JsonNode, bool> match)
	{
		return (await GetAsync(library)).AsArray().FirstOrDefault(item => match(item!));
	}

	private async Task DeleteAsync(string path)
	{
		using var response = await http.DeleteAsync($"api/v3/{path}");
		response.EnsureSuccessStatusCode();
	}
}
