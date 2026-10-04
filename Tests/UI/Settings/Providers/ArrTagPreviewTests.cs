using Domain.Wantlist.Arr;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ArrTagPreviewTests
{
	private static readonly ArrTagUser Guest = new(Guid.NewGuid(), "Guest");

	private static readonly ArrTagUser Daerup = new(Guid.NewGuid(), "Daerup");

	private static readonly ArrTagFeed Latest = new(Guid.NewGuid(), "Latest movies", "snoak/latest");

	[Test]
	public async Task Rows_ForTwoUsersAndAFeed_ShowTheViewersTitleTheirFeedsTitleThenTheOtherUsers()
	{
		var rows = ArrTagPreview.Rows(ArrTagScheme.Default, [Guest, Daerup], Some.Of(Latest));

		await Assert.That(rows.Select(row => row.Title)).IsEquivalentTo(["Guest", "Latest movies", "Daerup"], CollectionOrdering.Matching);
		await Assert.That(rows[1].Detail).IsEqualTo("From Guest's feed");
		await Assert.That(rows[1].Labels).IsEquivalentTo(["sammlarr-u-guest", "sammlarr-latest-movies"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Rows_WithoutAFeed_ShowOnlyTitlesAddedByHand()
	{
		var rows = ArrTagPreview.Rows(ArrTagScheme.Default, [Guest], new None());

		await Assert.That(rows.Select(row => row.Detail)).IsEquivalentTo(["Added by hand"]);
	}
}
