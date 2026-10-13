using Shared.Options;

namespace UI.Shared.Services;

internal sealed class AppVersionProvider(ReleaseOptions options, AppVersion current)
{
	public AppVersion Current => current;

	public string Repository => options.GitHubRepo;
}
