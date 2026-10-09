using System.Diagnostics;
using Domain.Feed;
using Observability.Feed;
using TestInfrastructure;

namespace Shared.Tests.Observability;

public class FeedRunSpansTests
{
	[Test]
	public async Task Title_InsideARun_LeavesTheCurrentSpanAlone()
	{
		using var listener = Listen();
		_ = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());

		var title = FeedRunSpans.Title("Heat", Some.Of(1995));

		await Assert.That(title).IsSome();
		await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Run);
	}

	[Test]
	public async Task Enter_RestoresTheOuterSpanOnDispose()
	{
		using var listener = Listen();
		_ = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());
		var title = FeedRunSpans.Title("Heat", new None());

		using (FeedRunSpans.Enter(title))
		{
			await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Title);
		}

		await Assert.That(Activity.Current?.OperationName).IsEqualTo(FeedRunNames.Run);
	}

	private static ActivityListener Listen()
	{
		var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == FeedRunSpans.SourceName,
			Sample = (ref _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);
		return listener;
	}
}
