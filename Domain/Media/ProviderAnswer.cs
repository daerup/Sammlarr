using Domain.Providers;

namespace Domain.Media;

public sealed record ProviderAnswer(MediaProvider Provider, MediaFields Unfilled, DateTimeOffset AnsweredAt)
{
	public static ProviderAnswer After(MediaProvider provider, MediaFields fills, MediaDetails answer, DateTimeOffset at)
	{
		return new(provider, fills & answer.Missing, at);
	}
}
