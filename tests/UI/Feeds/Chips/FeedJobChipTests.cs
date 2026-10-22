using Bunit;
using Domain.Jobs;
using UI.Features.Feeds.Components.Chips;

namespace UI.Tests.Feeds.Chips;

public class FeedJobChipTests : BunitContext
{
	[Test]
	[Arguments(JobState.Queued)]
	[Arguments(JobState.Waiting)]
	public async Task Render_ForAJobInTheQueue_SaysQueued(JobState state)
	{
		var cut = Render<FeedJobChip>(parameters => parameters.Add(chip => chip.Job, Some.Of(state)));

		await Assert.That(cut.Find(".feed-chip").TextContent.Trim()).IsEqualTo("Queued");
	}

	[Test]
	[Arguments(JobState.Running)]
	[Arguments(JobState.Done)]
	[Arguments(JobState.Failed)]
	[Arguments(JobState.Cancelled)]
	public async Task Render_ForAJobNotWaiting_ShowsNoChip(JobState state)
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
