using Bunit;
using Domain.Jobs;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedJobChipTests : BunitContext
{
	[Test]
	[Arguments(JobState.Running, "Running")]
	[Arguments(JobState.Queued, "Queued")]
	[Arguments(JobState.Waiting, "Queued")]
	public async Task Render_ForAJobStillToFinish_NamesWhereItIs(JobState state, string expected)
	{
		var cut = Render<FeedJobChip>(parameters => parameters.Add(chip => chip.Job, Some.Of(state)));

		await Assert.That(cut.Find(".feed-chip").TextContent.Trim()).IsEqualTo(expected);
	}

	[Test]
	[Arguments(JobState.Done)]
	[Arguments(JobState.Failed)]
	[Arguments(JobState.Cancelled)]
	public async Task Render_ForASettledJob_ShowsNoChip(JobState state)
	{
		var cut = Render<FeedJobChip>(parameters => parameters.Add(chip => chip.Job, Some.Of(state)));

		await Assert.That(cut.FindAll(".feed-chip")).IsEmpty();
	}

	[Test]
	public async Task Render_WithoutAJob_ShowsNoChip()
	{
		var cut = Render<FeedJobChip>();

		await Assert.That(cut.FindAll(".feed-chip")).IsEmpty();
	}
}
