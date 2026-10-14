using Domain.Providers;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class ListTextsTests
{
	[Test]
	[Arguments(SinkKind.Plex, "Plex watchlist")]
	[Arguments(SinkKind.Arr, "Radarr and Sonarr")]
	public async Task EmptyWantlist_SaysWhereATitleGoes(SinkKind sink, string where)
	{
		await Assert.That(ListTexts.EmptyWantlist(sink)).Contains(where).And.DoesNotContain("Ctrl");
	}
}
