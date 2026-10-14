using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace UI.Shared.Services;

public sealed partial record AppVersion(string Number)
{
	public bool IsDevBuild => DevBuild.IsMatch(Number);

	public bool HasRelease => !Number.StartsWith("0.0.0-", StringComparison.Ordinal);

	public int CommitsSinceRelease => IsDevBuild ? int.Parse(DevBuild.Match(Number).Groups["commits"].Value, CultureInfo.InvariantCulture) : 0;

	internal static AppVersion From(string informationalVersion) => new(informationalVersion.Split('+')[0]);

	internal static AppVersion Running() => From(Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

	[GeneratedRegex(@"-alpha\.0\.(?<commits>\d+)$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex DevBuild { get; }
}
