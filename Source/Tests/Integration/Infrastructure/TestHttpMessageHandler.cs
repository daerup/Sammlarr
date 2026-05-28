using System.Net;
using System.Text;
using System.Web;

namespace Integration.Tests.Infrastructure;

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
	private Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

	private readonly List<(string UrlContains, Func<HttpRequestMessage, bool> Validator)> _credentialValidators = [];

	private readonly Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _urlResponders = new();

	public void RespondWithJson(string json) =>
		_respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

	public void RespondWithJsonForUrl(string urlContains, string json) =>
		_urlResponders[urlContains] = _ =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

	public void RespondWith(HttpStatusCode statusCode) => _respond = _ => Task.FromResult(new HttpResponseMessage(statusCode));

	public void ClearUrlStubs() => _urlResponders.Clear();

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
		var url = request.RequestUri?.ToString() ?? string.Empty;

		foreach (var (urlContains, validator) in _credentialValidators)
		{
			if (url.Contains(urlContains, StringComparison.OrdinalIgnoreCase) && !validator(request))
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
		}

		foreach (var (pattern, responder) in _urlResponders)
		{
			if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				return responder(request);
		}

		return _respond(request);
	}
}
