using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace Integration.Tests.Infrastructure.Media.Local;

public sealed class LocalMediaContext
{
	public Dictionary<string, MediaId> MediaIds { get; } = new();

	public Dictionary<string, IReadOnlyDictionary<MediaProvider, string>> Ids { get; } = new();

	public void Record(CatalogEntry entry, MediaId id)
	{
		var key = entry.Alias ?? PrimaryToken(entry);
		MediaIds[key] = id;
		Ids[key] = entry.Ids;
	}

	private static string PrimaryToken(CatalogEntry entry)
	{
		var (provider, value) = entry.Ids.First();
		return $"{provider}:{value}";
	}
}
