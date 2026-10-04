using System.Text.Json.Nodes;

namespace E2E.Tests.PlexFree;

internal sealed class ArrApi(HttpClient http)
{
	private const string Key = "0123456789abcdef0123456789abcdef";

	private static readonly TimeSpan SettleTimeout = TimeSpan.FromMinutes(1);

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

	internal async Task SettleAsync()
	{
		var deadline = DateTimeOffset.UtcNow + SettleTimeout;
		for (var busy = await BusyAsync(); busy.Count > 0; busy = await BusyAsync())
		{
			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new TimeoutException($"{http.BaseAddress} still runs {string.Join(", ", busy)} after {SettleTimeout}");
			}

			await Task.Delay(250);
		}
	}

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

	internal static ArrApi For(Uri url)
	{
		var http = new HttpClient { BaseAddress = url };
		http.DefaultRequestHeaders.Add("X-Api-Key", Key);
		return new ArrApi(http);
	}

	private async Task DeleteAsync(string path)
	{
		using var response = await http.DeleteAsync($"api/v3/{path}");
		response.EnsureSuccessStatusCode();
	}
}
