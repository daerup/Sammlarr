using Contracts.Updates;
using Semver;
using Shared.Options;

namespace UI.Shared.Services;

public sealed record AppUpdate(string Version, Uri Page);

internal sealed class AppUpdateChecker(AppVersionProvider versions, ReleaseOptions options, IReleaseFeed releases, TimeProvider clock)
{
	private readonly Lock _gate = new();

	private Optional<Check> _checked = new None();

	public Task<Optional<AppUpdate>> CheckAsync() => CheckAsync(options.CheckInterval);

	public Task<Optional<AppUpdate>> CheckNowAsync() => CheckAsync(TimeSpan.Zero);

	private Task<Optional<AppUpdate>> CheckAsync(TimeSpan kept)
	{
		var running = versions.Current.Semantic;
		if (running.IsPrerelease || string.IsNullOrWhiteSpace(versions.Repository))
		{
			return Task.FromResult<Optional<AppUpdate>>(new None());
		}

		lock (_gate)
		{
			var now = clock.GetUtcNow();
			var fresh = _checked.When(check => now - check.At < kept).GetValueOr(() => new Check(now, AskAsync(running)));
			_checked = Some.Of(fresh);
			return fresh.Update;
		}
	}

	private async Task<Optional<AppUpdate>> AskAsync(SemVersion running)
	{
		var latest = await releases.LatestAsync(versions.Repository);
		return latest.Map(release => Newer(release, running));
	}

	private static Optional<AppUpdate> Newer(PublishedRelease release, SemVersion running)
	{
		return Optional.When(
			SemVersion.TryParse(release.Tag, SemVersionStyles.AllowV, out var offered) && !offered.IsPrerelease && offered.ComparePrecedenceTo(running) > 0,
			() => new AppUpdate(offered!.ToString(), release.Page)
		);
	}

	private sealed record Check(DateTimeOffset At, Task<Optional<AppUpdate>> Update);
}
