using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Web;
using API.Http;
using Domain.Usage;

namespace Harness;

internal sealed class HttpStub : HttpMessageHandler
{
	private readonly List<(string UrlContains, Func<HttpRequestMessage, bool> Validator)> _credentialValidators = [];

	private readonly Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _urlResponders = [];

	private readonly ConcurrentQueue<string> _requested = new();

	private readonly ConcurrentQueue<(string Url, UsageCause Cause)> _causes = new();

	private readonly ConcurrentQueue<string> _unexpected = new();

	private readonly ConcurrentQueue<string> _seen = new();

	public IReadOnlyCollection<string> Unexpected => _unexpected;

	public bool Saw(string text) => _seen.Any(seen => seen.Contains(text, StringComparison.Ordinal));

	public int Requests(string urlContains) => _requested.Count(url => url.Contains(urlContains, StringComparison.OrdinalIgnoreCase));

	public IReadOnlyList<UsageCause> Causes(string urlContains)
	{
		return [.. _causes.Where(sent => sent.Url.Contains(urlContains, StringComparison.OrdinalIgnoreCase)).Select(sent => sent.Cause)];
	}

	public void RespondWithJsonForUrl(string urlContains, string json) =>
		_urlResponders[urlContains] = _ =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

	public void RespondForUrl(string urlContains, Func<HttpRequestMessage, HttpResponseMessage> responder) =>
		_urlResponders[urlContains] = req => Task.FromResult(responder(req));

	public void RespondForUrlAsync(string urlContains, Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) => _urlResponders[urlContains] = responder;

	public void Unexpect(string what) => _unexpected.Enqueue(what);

	public void RespondWithStatusForUrl(string urlContains, HttpStatusCode statusCode) =>
		_urlResponders[urlContains] = _ => Task.FromResult(new HttpResponseMessage(statusCode));

	public HeldRequest Hold(string urlContains)
	{
		var respond = _urlResponders[urlContains];
		var held = new HeldRequest();
		_urlResponders[urlContains] = async request =>
		{
			await held.HoldAsync();
			return await respond(request);
		};
		return held;
	}

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

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
		_requested.Enqueue(url);
		_causes.Enqueue((url, UsageStamp.Of(request).Map(stamp => stamp.Mark.Cause).GetValueOr(UsageCause.Other)));
		_seen.Enqueue(await SeenAsync(request, cancellationToken));

		foreach (var (urlContains, validator) in _credentialValidators)
		{
			if (url.Contains(urlContains, StringComparison.OrdinalIgnoreCase) && !validator(request))
				return new HttpResponseMessage(HttpStatusCode.Unauthorized);
		}

		foreach (var (pattern, responder) in _urlResponders)
		{
			if (MatchesPattern(url, pattern))
				return await responder(request).WaitAsync(cancellationToken);
		}

		_unexpected.Enqueue(url);
		return new HttpResponseMessage(HttpStatusCode.NotImplemented);
	}

	private static async Task<string> SeenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
		return $"{request.RequestUri} {request.Headers} {request.Content?.Headers} {body}";
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
