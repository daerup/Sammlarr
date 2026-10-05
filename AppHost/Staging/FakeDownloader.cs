using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppHost.Staging;

internal sealed class FakeDownloader(string downloads, string stub, ILogger<FakeDownloader> logger) : BackgroundService
{
	private static readonly TimeSpan Settled = TimeSpan.FromSeconds(2);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				DownloadGrabs();
			}
			catch (IOException error)
			{
				logger.LogWarning(error, "The fake downloader skipped a pass over {Downloads}", downloads);
			}
		}
	}

	private void DownloadGrabs()
	{
		var settled = DateTime.UtcNow - Settled;
		foreach (var app in Directory.EnumerateDirectories(downloads))
		{
			foreach (var nzb in Directory.EnumerateFiles(Path.Combine(app, "nzb"), "*.nzb").Where(file => File.GetLastWriteTimeUtc(file) < settled))
			{
				var release = Path.GetFileNameWithoutExtension(nzb);
				var folder = Directory.CreateDirectory(Path.Combine(app, "watch", release));
				File.Copy(stub, Path.Combine(folder.FullName, $"{release}.mkv"), overwrite: true);
				File.Delete(nzb);
				logger.LogInformation("Downloaded {Release} into {App}'s watch folder", release, Path.GetFileName(app));
			}
		}
	}
}
