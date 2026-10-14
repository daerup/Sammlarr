using System.Text.Json.Nodes;

namespace Web.Staging;

internal sealed class ArrClient(HttpClient http)
{
	private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

	internal async Task<JsonNode> GetAsync(string path, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync($"api/v3/{path}", cancellationToken);
		response.EnsureSuccessStatusCode();
		return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;
	}

	internal async Task<JsonNode> PostAsync(string path, object body, CancellationToken cancellationToken)
	{
		using var response = await http.PostAsJsonAsync($"api/v3/{path}", body, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException(
				$"POST {http.BaseAddress}api/v3/{path} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}"
			);
		}

		return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;
	}

	internal async Task<bool> IsEmptyAsync(string path, CancellationToken cancellationToken)
	{
		return (await GetAsync(path, cancellationToken)).AsArray().Count == 0;
	}

	internal async Task<int> IdOfAsync(string path, string property, string value, CancellationToken cancellationToken)
	{
		var found = (await GetAsync(path, cancellationToken)).AsArray().Single(item => item![property]!.GetValue<string>() == value)!;
		return found["id"]!.GetValue<int>();
	}

	internal async Task WaitUntilUpAsync(CancellationToken cancellationToken)
	{
		using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		patience.CancelAfter(Patience);
		while (!await AnswersPingAsync(patience.Token))
		{
			await Task.Delay(TimeSpan.FromSeconds(1), patience.Token);
		}
	}

	private async Task<bool> AnswersPingAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var response = await http.GetAsync("ping", cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
	}

	internal static ArrClient For(string url, string apiKey)
	{
		var http = new HttpClient { BaseAddress = new Uri($"{url.TrimEnd('/')}/") };
		http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
		return new ArrClient(http);
	}
}
