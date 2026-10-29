using Bunit;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class LibrarySummaryTests : BunitContext
{
	[Test]
	public async Task Render_WithTitlesFeedsAndBlocklist_ReadsAsOneSentence()
	{
		var cut = Render<LibrarySummary>(parameters =>
			parameters.Add(summary => summary.Wanted, 59).Add(summary => summary.Feeds, 1).Add(summary => summary.Blocklisted, 1)
		);

		await Assert.That(Text(cut)).IsEqualTo("59 titles on your wantlist, assembled by 1 feed. 1 blocklisted.");
	}

	[Test]
	public async Task Render_WithNothingBlocklisted_EndsAfterTheFeeds()
	{
		var cut = Render<LibrarySummary>(parameters =>
			parameters.Add(summary => summary.Wanted, 1).Add(summary => summary.Feeds, 2).Add(summary => summary.Blocklisted, 0)
		);

		await Assert.That(Text(cut)).IsEqualTo("1 title on your wantlist, assembled by 2 feeds.");
	}

	private static string Text(IRenderedComponent<LibrarySummary> cut) => string.Concat(cut.Nodes.Select(node => node.TextContent)).Words();
}
