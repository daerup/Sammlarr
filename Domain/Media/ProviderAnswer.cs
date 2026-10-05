using Domain.Providers;

namespace Domain.Media;

public sealed record ProviderAnswer(MediaProvider Provider, MediaFields Unfilled)
{
	public static ProviderAnswer After(MediaProvider provider, MediaFields fills, MediaDetails merged) => new(provider, fills & merged.Missing);
}
