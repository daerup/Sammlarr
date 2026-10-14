using System.Collections.Concurrent;

namespace Web.Instrumentation;

internal sealed class BlazorRouteTracker
{
	private readonly ConcurrentDictionary<string, string> _urls = new(StringComparer.Ordinal);

	public string? UpdateAndGet(string connectionId, string hubMethodName, IReadOnlyList<object?> arguments)
	{
		var extracted = TryExtract(hubMethodName, arguments);
		if (extracted is not null)
		{
			_urls[connectionId] = extracted;
		}
		return _urls.TryGetValue(connectionId, out var url) ? url : null;
	}

	public void Remove(string connectionId) => _urls.TryRemove(connectionId, out _);

	public static string? GetFriendlyLabel(string hubMethodName) =>
		hubMethodName switch
		{
			"OnLocationChanged" => "NAVIGATE",
			"StartCircuit" => "OPEN",
			"UpdateRootComponents" => "RENDER",
			"DispatchBrowserEvent" => "EVENT",
			_ => null,
		};

	private static string? TryExtract(string hubMethodName, IReadOnlyList<object?> arguments)
	{
		var raw = hubMethodName switch
		{
			"OnLocationChanged" => arguments.Count > 0 ? arguments[0] as string : null,
			"StartCircuit" => arguments.Count > 1 ? arguments[1] as string : null,
			_ => null,
		};
		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}
		return Uri.TryCreate(raw, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : raw;
	}
}
