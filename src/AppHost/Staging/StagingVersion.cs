using System.Diagnostics;

namespace AppHost.Staging;

internal static class StagingVersion
{
	internal const string BuildArgument = "VERSION";

	internal static async Task<string> ReadAsync(string repositoryRoot, CancellationToken cancellationToken)
	{
		using var minVer = Process.Start(
			new ProcessStartInfo("dotnet", ["msbuild", "src/Web/Web.csproj", "-nologo", "-target:MinVer", "-getProperty:MinVerVersion"])
			{
				WorkingDirectory = repositoryRoot,
				RedirectStandardOutput = true,
				UseShellExecute = false,
			}
		)!;
		var output = await minVer.StandardOutput.ReadToEndAsync(cancellationToken);
		await minVer.WaitForExitAsync(cancellationToken);
		return minVer.ExitCode == 0 ? output.Trim() : throw new InvalidOperationException($"MinVer could not work out the version: {output.Trim()}");
	}
}
