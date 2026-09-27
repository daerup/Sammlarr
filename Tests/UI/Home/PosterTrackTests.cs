using Bunit;
using Domain.Media;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class PosterTrackTests : BunitContext
{
	private static readonly MediaId First = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Second = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Fresh = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	[Test]
	public async Task Ids_WhenANewTitleIsPrependedAfterAReload_RequestsJustThatTitle()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		var requests = new List<IReadOnlyList<MediaId>>();
		var cut = Render<PosterTrack>(parameters =>
			parameters
				.Add(track => track.Ids, [First, Second])
				.Add(track => track.Media, new Dictionary<MediaId, MediaLookup>())
				.Add(track => track.OnNeedMedia, ids => requests.Add(ids))
		);

		cut.Render(parameters => parameters.Add(track => track.Ids, [Fresh, First, Second]));

		await Assert.That(requests).Count().IsEqualTo(2);
		await Assert.That(requests[1]).IsEquivalentTo([Fresh]);
	}
}
