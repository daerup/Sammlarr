using System.Runtime.CompilerServices;
using Domain.Media;

namespace UI.Tests.Feeds;

internal static class HeldItems
{
	internal static async IAsyncEnumerable<Listed<MediaMatchResult>> UntilAsync(Task loaded, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await loaded.WaitAsync(cancellationToken);
		yield break;
	}
}
