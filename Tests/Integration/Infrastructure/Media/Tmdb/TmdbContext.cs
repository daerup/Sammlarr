using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Infrastructure.Media.Tmdb;

public sealed class TmdbContext
{
	public Dictionary<string, ResolvedVersion> Versions { get; } = new();
}
