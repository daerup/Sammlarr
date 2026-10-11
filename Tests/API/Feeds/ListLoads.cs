using System.Runtime.CompilerServices;
using Contracts.Media;
using Domain.Media;

namespace API.Tests.Feeds;

internal static class ListLoads
{
	internal static async IAsyncEnumerable<Listed<MediaMatchResult>> UnresolvedAsync(
		IAsyncEnumerable<ListTitle> entries,
		Action<IdentifyRequest> asked,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		var position = 0;
		await foreach (var entry in entries.WithCancellation(cancellationToken))
		{
			yield return new Listed<MediaMatchResult>(position++, Settled(entry, asked));
		}
	}

	private static MediaMatchResult Settled(ListTitle entry, Action<IdentifyRequest> asked)
	{
		return entry switch
		{
			IdentifyRequest request => Asked(request, asked),
			UnmatchedMedia unmatched => unmatched,
		};
	}

	private static UnmatchedMedia Asked(IdentifyRequest request, Action<IdentifyRequest> asked)
	{
		asked(request);
		return new UnmatchedMedia([.. request.Candidates], MediaDetails.Empty(request.Candidates[0].Type));
	}
}
