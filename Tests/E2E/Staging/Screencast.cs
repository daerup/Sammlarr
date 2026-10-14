using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;

namespace E2E.Tests.Staging;

internal sealed class Screencast(ICDPSession session, DirectoryInfo folder, ViewportSize viewport)
{
	private const string Palette =
		"fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";

	private const double Hold = 1.5;

	private readonly ConcurrentQueue<Frame> _frames = new();
	private int _count;

	internal double Latest => _frames.Select(frame => frame.Timestamp).DefaultIfEmpty().Max();

	internal static async Task<Screencast> StartAsync(IPage page, DirectoryInfo frames)
	{
		var size = page.ViewportSize!;
		var cdp = await page.Context.NewCDPSessionAsync(page);
		var screencast = new Screencast(cdp, frames, new ViewportSize { Width = size.Width, Height = size.Height });
		cdp.Event("Page.screencastFrame").OnEvent += (_, frame) => screencast.Capture(frame!.Value);
		await cdp.SendAsync(
			"Page.startScreencast",
			new()
			{
				["format"] = "png",
				["maxWidth"] = size.Width,
				["maxHeight"] = size.Height,
				["everyNthFrame"] = 1,
			}
		);
		return screencast;
	}

	internal async Task StopAsync()
	{
		await session.SendAsync("Page.stopScreencast");
		await session.DetachAsync();
	}

	internal async Task EncodeGifAsync(Crop crop, double from, string gif)
	{
		var ordered = _frames.OrderBy(frame => frame.Timestamp).ToList();
		var kept = ordered.Skip(Math.Max(0, ordered.FindLastIndex(frame => frame.Timestamp <= from))).ToList();
		var list = Path.Combine(folder.FullName, "frames.txt");
		await File.WriteAllTextAsync(list, string.Join('\n', Concat(kept, from)));
		await FfmpegAsync(list, $"{crop.Filter(viewport)},{Palette}", gif);
	}

	private void Capture(JsonElement frame)
	{
		_ = session.SendAsync("Page.screencastFrameAck", new() { ["sessionId"] = frame.GetProperty("sessionId").GetInt32() });
		var file = $"frame-{Interlocked.Increment(ref _count):D5}.png";
		File.WriteAllBytes(Path.Combine(folder.FullName, file), Convert.FromBase64String(frame.GetProperty("data").GetString()!));
		_frames.Enqueue(new Frame(file, frame.GetProperty("metadata").GetProperty("timestamp").GetDouble()));
	}

	private static IEnumerable<string> Concat(List<Frame> kept, double from)
	{
		yield return "ffconcat version 1.0";
		for (var index = 0; index < kept.Count; index++)
		{
			var shown = Math.Max(kept[index].Timestamp, from);
			var next = index + 1 < kept.Count ? kept[index + 1].Timestamp : shown + Hold;
			yield return $"file '{kept[index].File}'";
			yield return string.Create(CultureInfo.InvariantCulture, $"duration {next - shown:0.0000}");
		}

		yield return $"file '{kept[^1].File}'";
	}

	private static async Task FfmpegAsync(string list, string filter, string gif)
	{
		using var ffmpeg = Process.Start(
			new ProcessStartInfo("ffmpeg")
			{
				RedirectStandardError = true,
				UseShellExecute = false,
				ArgumentList = { "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", list, "-vf", filter, "-loop", "0", gif },
			}
		)!;
		var errors = await ffmpeg.StandardError.ReadToEndAsync();
		await ffmpeg.WaitForExitAsync();
		if (ffmpeg.ExitCode != 0)
		{
			throw new InvalidOperationException($"ffmpeg stopped with exit code {ffmpeg.ExitCode}: {errors}");
		}
	}

	private sealed record Frame(string File, double Timestamp);
}

internal readonly record struct Crop(double Left, double Top, double Right, double Bottom)
{
	internal static Crop Of(LocatorBoundingBoxResult box) => new(box.X, box.Y, box.X + box.Width, box.Y + box.Height);

	internal Crop Union(Crop other)
	{
		return new(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
	}

	internal string Filter(ViewportSize frame)
	{
		var left = (int)Math.Clamp(Math.Floor(Left), 0, frame.Width);
		var top = (int)Math.Clamp(Math.Floor(Top), 0, frame.Height);
		var right = (int)Math.Clamp(Math.Ceiling(Right), left, frame.Width);
		var bottom = (int)Math.Clamp(Math.Ceiling(Bottom), top, frame.Height);
		return string.Create(CultureInfo.InvariantCulture, $"crop={right - left}:{bottom - top}:{left}:{top}");
	}
}
