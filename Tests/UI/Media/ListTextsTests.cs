using Domain.Providers;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class ListTextsTests
{
	[Test]
	[Arguments(SinkKind.Plex, "go to your Plex watchlist on the next sync")]
	[Arguments(SinkKind.Arr, "go to Radarr and Sonarr on the next sync")]
	public async Task EmptyWantlist_SaysWhereATitleGoes(SinkKind sink, string where)
	{
		await Assert.That(ListTexts.EmptyWantlist(sink)).Contains(where).And.DoesNotContain("Ctrl");
	}
}
