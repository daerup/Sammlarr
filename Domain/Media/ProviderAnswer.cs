using Domain.Providers;

namespace Domain.Media;

public sealed record ProviderAnswer(RemoteService Provider, MediaFields Unfilled, DateTimeOffset AnsweredAt)
{
	public static ProviderAnswer After(RemoteService provider, MediaFields fills, MediaDetails answer, DateTimeOffset at)
	{
		return new(provider, fills & answer.Missing, at);
	}
}
