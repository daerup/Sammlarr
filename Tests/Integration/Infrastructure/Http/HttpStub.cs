using System.Net;
using System.Text;
using System.Web;

namespace Integration.Tests.Infrastructure.Http;

internal sealed class HttpStub : HttpMessageHandler
{
	private Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

	private readonly List<(string UrlContains, Func<HttpRequestMessage, bool> Validator)> _credentialValidators = [];

	private readonly Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _urlResponders = new();

	public void RespondWithJson(string json) =>
		_respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

	public void RespondWithJsonForUrl(string urlContains, string json) =>
		_urlResponders[urlContains] = _ =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

	public void RespondForUrl(string urlContains, Func<HttpRequestMessage, HttpResponseMessage> responder) =>
		_urlResponders[urlContains] = req => Task.FromResult(responder(req));

	public void RespondWithStatusForUrl(string urlContains, HttpStatusCode statusCode) =>
		_urlResponders[urlContains] = _ => Task.FromResult(new HttpResponseMessage(statusCode));

	public void RequireQueryParam(string urlContains, string name, string value) =>
		_credentialValidators.Add(
			(
				urlContains,
				req =>
				{
					var query = HttpUtility.ParseQueryString(req.RequestUri?.Query ?? string.Empty);
					return query[name] == value;
				}
			)
		);

	public void RequireHeader(string urlContains, string name, string value) =>
		_credentialValidators.Add((urlContains, req => req.Headers.TryGetValues(name, out var values) && values.Contains(value)));

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

		foreach (var (urlContains, validator) in _credentialValidators)
		{
			if (url.Contains(urlContains, StringComparison.OrdinalIgnoreCase) && !validator(request))
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
		}

		foreach (var (pattern, responder) in _urlResponders)
		{
			if (MatchesPattern(url, pattern))
				return responder(request);
		}

		return _respond(request);
	}

	private static bool MatchesPattern(string url, string pattern)
	{
		var index = url.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
		while (index >= 0)
		{
			var end = index + pattern.Length;
			if (end == url.Length || !char.IsLetterOrDigit(url[end]))
				return true;
			index = url.IndexOf(pattern, index + 1, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
