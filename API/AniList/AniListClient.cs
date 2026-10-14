using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using API.AniList.Types;
using API.Http;
using Domain.Providers;

namespace API.AniList;

internal sealed class AniListClient(HttpClient http)
{
	public async Task<Result<TData>> AskAsync<TData>(string query, JsonObject variables, CancellationToken cancellationToken = default)
		where TData : class
	{
		using var content = JsonContent.Create(new JsonObject { ["query"] = query, ["variables"] = variables });
		using var response = await http.PostAsync(string.Empty, content, cancellationToken);
		if (response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
		{
			return response.StatusCode.Unanswered(RemoteService.AniList, $"AniList answered {(int)response.StatusCode}.");
		}

		var answer = await response.Content.ReadFromJsonAsync<AniListTypes.Answer<TData>>(cancellationToken);
		var problems = answer?.Errors ?? [];
		if (problems.Count > 0)
		{
			return Error.From(new AniListAnswerException(string.Join("; ", problems.Select(problem => problem.Message))));
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.AniList, $"AniList answered {(int)response.StatusCode}.");
		}

		return Optional.Of(answer?.Data).ToResult("AniList answered without data.");
	}
}

internal sealed class AniListAnswerException(string message) : Exception(message);
