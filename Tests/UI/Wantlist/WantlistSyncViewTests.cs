using Domain.Media;
using Domain.Wantlist.Sync;
using UI.Features.Wantlist.Components;

namespace UI.Tests.Wantlist;

public class WantlistSyncViewTests
{
	private static readonly MediaId Pulled = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Pushed = MediaId.From(new Guid(2, 0, 0, new byte[8]));

	[Test]
	public async Task Items_ForChangesBothWays_SayWhichSinkEachWentThrough()
	{
		var summary = new WantlistSyncSummary(new WantlistDelta([Pulled], []), new WantlistDelta([Pushed], []), []);

		var items = WantlistSyncView.Items(
			summary,
			new Dictionary<MediaId, MatchedMedia> { [Pulled] = Matched(Pulled), [Pushed] = Matched(Pushed) },
			"Radarr + Sonarr"
		);

		string?[] tooltips = ["Pulled from Radarr + Sonarr", "Pushed to Radarr + Sonarr"];
		await Assert.That(items.Select(item => item.ContextTooltip)).IsEquivalentTo(tooltips);
	}

	private static MatchedMedia Matched(MediaId id) => new(new MediaIdentity(id, [TmdbId.From("1", MediaType.Movie)]), new MovieDetails { Title = "Title" });
}
