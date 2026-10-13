namespace Web.Health;

internal static class ContainerHealthProbe
{
	private const string DefaultPort = "6769";

	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

	public static async Task<int> CheckAsync(HttpMessageHandler handler, string? ports)
	{
		using var client = new HttpClient(handler) { Timeout = Patience };
		try
		{
			using var response = await client.GetAsync(Alive(ports));
			return response.IsSuccessStatusCode ? 0 : 1;
		}
		catch (Exception unreachable) when (unreachable is HttpRequestException or TaskCanceledException)
		{
			return 1;
		}
	}

	internal static Uri Alive(string? ports) => new($"http://localhost:{(ports ?? DefaultPort).Split(';')[0]}/alive");
}
