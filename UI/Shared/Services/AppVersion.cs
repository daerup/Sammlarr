using System.Globalization;
using System.Text.RegularExpressions;

namespace UI.Shared.Services;

public sealed partial record AppVersion(string Number)
{
	public bool IsDevBuild => DevBuild().IsMatch(Number);

	public bool HasRelease => !Number.StartsWith("0.0.0-", StringComparison.Ordinal);

	public int CommitsSinceRelease => IsDevBuild ? int.Parse(DevBuild().Match(Number).Groups["commits"].Value, CultureInfo.InvariantCulture) : 0;

	internal static AppVersion From(string informationalVersion) => new(informationalVersion.Split('+')[0]);

	[GeneratedRegex(@"-alpha\.0\.(?<commits>\d+)$")]
	private static partial Regex DevBuild();
}
