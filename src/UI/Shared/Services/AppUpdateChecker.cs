using Contracts;
using Contracts.Updates;
using Shared.Options;

namespace UI.Shared.Services;

public sealed record AppUpdate(string Version, Uri Page);

internal sealed class AppUpdateChecker(AppVersionProvider versions, ReleaseOptions options, IReleaseFeed releases, IDateTimeProvider clock)
{
	private readonly Lock _gate = new();

	private Optional<Check> _checked = new None();

	public Task<Optional<AppUpdate>> CheckAsync() => CheckAsync(options.CheckInterval);

	public Task<Optional<AppUpdate>> CheckNowAsync() => CheckAsync(TimeSpan.Zero);

	private Task<Optional<AppUpdate>> CheckAsync(TimeSpan kept)
	{
		if (!Version.TryParse(versions.Current.Number, out var running) || string.IsNullOrWhiteSpace(versions.Repository))
		{
			return Task.FromResult<Optional<AppUpdate>>(new None());
		}

		lock (_gate)
		{
			var now = clock.Now;
			var fresh = _checked.When(check => now - check.At < kept).GetValueOr(() => new Check(now, AskAsync(running)));
			_checked = Some.Of(fresh);
			return fresh.Update;
		}
	}

	private async Task<Optional<AppUpdate>> AskAsync(Version running)
	{
		var latest = await releases.LatestAsync(versions.Repository);
		return latest.Map(release => Newer(release, running));
	}

	private static Optional<AppUpdate> Newer(PublishedRelease release, Version running)
	{
		var tag = release.Tag.TrimStart('v');
		return Optional.When(Version.TryParse(tag, out var offered) && offered > running, () => new AppUpdate(tag, release.Page));
	}

	private sealed record Check(DateTimeOffset At, Task<Optional<AppUpdate>> Update);
}
