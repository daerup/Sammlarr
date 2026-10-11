using Microsoft.Extensions.Hosting;

namespace API.Http.Lanes;

internal sealed class LaneSettingsCheck(IEnumerable<RemoteSettings> settings) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		List<string> unknown =
		[
			.. from remote in settings
			from lane in remote.Options.Http.Lanes
			from route in lane.Value.Routes
			where !remote.Options.Urls.ContainsKey(route.Url)
			select $"{remote.Remote}'s lane {lane.Key} routes to the URL {route.Url}, which it does not have",
		];
		return unknown.Count == 0 ? Task.CompletedTask : Task.FromException(new InvalidOperationException(string.Join("; ", unknown)));
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
