using Domain.Feed;
using Domain.Usage;
using Observability.Usage;
using TestInfrastructure;

namespace Shared.Tests.Usage;

public class UsageCausesTests
{
	[Test]
	public async Task Current_WithoutAMark_IsOther()
	{
		await Assert.That(UsageCauses.Current.Cause).IsEqualTo(UsageCause.Other);
		await Assert.That(UsageCauses.Current.Feed).IsNone();
	}

	[Test]
	public async Task Mark_InsideAFeedRun_KeepsTheRunsCauseAndFeed()
	{
		var feed = FeedId.From(Guid.NewGuid());
		using var run = UsageCauses.Mark(feed);

		using (UsageCauses.Mark(UsageCause.ShowingTitles))
		{
			await Assert.That(UsageCauses.Current.Cause).IsEqualTo(UsageCause.FeedRun);
			await Assert.That(UsageCauses.Current.Feed).IsSome().And.IsEqualTo(feed);
		}

		await Assert.That(UsageCauses.Current.Cause).IsEqualTo(UsageCause.FeedRun);
	}

	[Test]
	public async Task Mark_ForTheFeedEditorWithAFeed_CarriesTheFeedButNoRun()
	{
		var feed = FeedId.From(Guid.NewGuid());

		using var marked = UsageCauses.Mark(UsageCause.FeedEditor, Some.Of(feed));

		await Assert.That(UsageCauses.Current).IsEqualTo(new UsageMark(UsageCause.FeedEditor, Some.Of(feed)));
		await Assert.That(UsageCauses.Current.Run).IsNone();
	}

	[Test]
	public async Task Mark_ForAFeedRun_NamesItsRun()
	{
		var feed = FeedId.From(Guid.NewGuid());

		using var marked = UsageCauses.Mark(feed);

		await Assert.That(UsageCauses.Current.Run).IsSome().And.IsEqualTo(feed);
	}

	[Test]
	public async Task Mark_OnceDisposed_LeavesNoCause()
	{
		using (UsageCauses.Mark(UsageCause.Search))
		{
			await Assert.That(UsageCauses.Current.Cause).IsEqualTo(UsageCause.Search);
		}

		await Assert.That(UsageCauses.Current.Cause).IsEqualTo(UsageCause.Other);
	}
}
