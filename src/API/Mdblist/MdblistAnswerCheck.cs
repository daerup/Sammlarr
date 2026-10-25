using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace API.Mdblist;

internal sealed class MdblistAnswerCheck(ILogger<MdblistAnswerCheck> logger) : DelegatingHandler
{
	private static readonly string[] QuotaWords = ["limit reached", "limit exceeded"];

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = await base.SendAsync(request, cancellationToken);
		if (response.StatusCode is not HttpStatusCode.OK)
		{
			return response;
		}

		await response.Content.LoadIntoBufferAsync(cancellationToken);
		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		return Problem(body).Match(problem => Refused(request, response, problem), () => response);
	}

	private HttpResponseMessage Refused(HttpRequestMessage request, HttpResponseMessage response, string problem)
	{
		logger.LogWarning(
			"MDBList answered 200 to {Method} {Path} with a body that is no answer: {Problem}",
			request.Method,
			request.RequestUri?.AbsolutePath,
			problem
		);
		var status = QuotaWords.Any(words => problem.Contains(words, StringComparison.OrdinalIgnoreCase))
			? HttpStatusCode.TooManyRequests
			: HttpStatusCode.BadGateway;
		var refused = new HttpResponseMessage(status) { RequestMessage = request, Content = new StringContent(problem) };
		foreach (var header in response.Headers)
		{
			refused.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		response.Dispose();
		return refused;
	}

	private static Optional<string> Problem(string body)
	{
		try
		{
			using var document = JsonDocument.Parse(body);
			return Optional.When(document.RootElement.ValueKind is JsonValueKind.Object, () => document.RootElement).Map(ErrorOf);
		}
		catch (JsonException unreadable)
		{
			return Some.Of($"not JSON ({unreadable.Message})");
		}
	}

	private static Optional<string> ErrorOf(JsonElement root)
	{
		var refused = root.TryGetProperty("response", out var answered) && answered.ValueKind is JsonValueKind.False;
		var error = Optional
			.When(root.TryGetProperty("error", out var said) && said.ValueKind is not JsonValueKind.Null, () => said.ToString())
			.When(text => text.Length > 0);
		return error.Or(refused, () => "response false");
	}
}
