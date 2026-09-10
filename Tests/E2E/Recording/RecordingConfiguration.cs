using Microsoft.Extensions.Configuration;

namespace E2E.Tests.Recording;

internal static class RecordingConfiguration
{
	private static readonly IConfiguration Configuration = new ConfigurationBuilder()
		.AddUserSecrets(typeof(RecordingConfiguration).Assembly, optional: true)
		.AddEnvironmentVariables()
		.Build();

	internal static string? Value(string key) => Configuration[key];
}
