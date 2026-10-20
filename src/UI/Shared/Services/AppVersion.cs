using System.Numerics;
using System.Reflection;
using Semver;

namespace UI.Shared.Services;

public sealed record AppVersion(string Number)
{
	public bool IsDevBuild => Height is not null;

	public bool HasRelease => Semantic is not { Major.IsZero: true, Minor.IsZero: true, Patch.IsZero: true, IsPrerelease: true };

	public int CommitsSinceRelease => (int)(Height ?? 0);

	internal SemVersion Semantic { get; } = SemVersion.Parse(Number, SemVersionStyles.Strict);

	private BigInteger? Height => Semantic.PrereleaseIdentifiers is [{ Value: "alpha" }, { Value: "0" }, { NumericValue: { } height }] ? height : null;

	internal static AppVersion From(string informationalVersion)
	{
		return new(SemVersion.Parse(informationalVersion, SemVersionStyles.Strict).WithoutMetadata().ToString());
	}

	internal static AppVersion Running() => From(Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
}
