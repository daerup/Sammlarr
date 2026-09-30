using System.Reflection;
using Shared.Options;

namespace UI.Shared.Services;

internal sealed class AppVersionProvider(ReleaseOptions options)
{
	public AppVersion Current { get; } =
		AppVersion.From(Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

	public string Repository => options.GitHubRepo;
}
