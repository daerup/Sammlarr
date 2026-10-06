using Microsoft.Extensions.Configuration;

namespace AppHost.Staging;

internal sealed record StagingConfig(string Host, string CatcherSession, StagingPorts Ports, StagingNetwork Network)
{
	internal const string SectionName = "Staging";

	internal const string PersonasSection = "Staging:Personas";

	internal const string SeedSection = "Staging:Seed";

	internal const string SammlarrSection = "Sammlarr";

	private const string FileName = "appsettings.Staging.json";

	internal static StagingConfig Load(IDistributedApplicationBuilder builder)
	{
		builder.Configuration.AddJsonFile(Path.Combine(builder.AppHostDirectory, FileName));
		return builder.Configuration.GetSection(SectionName).Get<StagingConfig>()!;
	}
}

internal sealed record StagingPorts(int Sammlarr, int Catcher, int WireMock, int Radarr, int Sonarr);

internal sealed record StagingNetwork(string DeadProxy, string[] Reachable);
