using System.Diagnostics;
using Core.Feed.Trace;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Observability.Feed;
using TestInfrastructure;

namespace Core.Tests.Feed;

public class FeedRunTraceTests
{
	private static readonly ActivitySource Http = new("System.Net.Http");

	private static readonly ActivitySource Untagged = new(FeedRunSpans.SourceName);

	private static readonly UserId Owner = UserId.From(Guid.NewGuid());

	private readonly FeedId _feed = FeedId.From(Guid.NewGuid());

	[Test]
	public async Task Title_OutsideARun_RecordsNothing()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);

		var title = FeedRunSpans.Title("Heat", Some.Of(1995));

		await Assert.That(title).IsNone();
		await Assert.That(testee.Read(_feed, Owner, TraceCursor.Start)).IsOfUnionType<TraceRead, NoTrace>();
	}

	[Test]
	public async Task Hop_OutsideATitle_RecordsNothing()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			using var hop = FeedRunSpans.Hop(RemoteService.Plex, TraceStage.Ids);
		}

		await Assert.That((await SnapshotAsync(testee)).Nodes).IsEmpty();
	}

	[Test]
	public async Task Read_AfterACursor_ReturnsEachEntryOnceInOrder()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Answered);
		}

		var run = (await SnapshotAsync(testee)).Cursor.Run;
		var all = await BatchAsync(testee, new TraceCursor(run, 0));
		var rest = await BatchAsync(testee, new TraceCursor(run, 2));

		await Assert.That(all.Steps.Select(step => step.Seq)).IsEquivalentTo([1L, 2L, 3L, 4L]);
		await Assert.That(rest.Steps.Select(step => step.Event)).AreOfUnionType<IEnumerable<TraceEvent>, TraceEvent>(typeof(HopAnswered), typeof(TitleSettled));
		await Assert.That(rest.Cursor.Seq).IsEqualTo(4);
	}

	[Test]
	public async Task Read_WithoutTheRunKey_ReturnsTheSnapshot()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using var run = testee.Start(_feed, Owner);
		var title = FeedRunSpans.Title("Heat", Some.Of(1995));
		using var entered = FeedRunSpans.Enter(title);
		using var hop = FeedRunSpans.Hop(RemoteService.Plex, TraceStage.Ids);

		var snapshot = await SnapshotAsync(testee);

		await Assert
			.That(snapshot.Titles)
			.IsEquivalentTo([new TitleInFlight(0, "Heat", Some.Of(1995), Some.Of(new HopPlace(RemoteService.Plex, TraceStage.Ids)), new None(), new None())]);
		await Assert.That(snapshot.Nodes.Single().Waiting).IsEqualTo(1);
		await Assert.That(snapshot.Fates.Started).IsEqualTo(1);
	}

	[Test]
	public async Task Record_PastTheCap_KeepsTitleEventsAndDropsHops()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance, 3);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Answered);
			Resolve("Ronin", HopOutcome.Answered);
		}

		var batch = await BatchAsync(testee, new TraceCursor((await SnapshotAsync(testee)).Cursor.Run, 0));

		await Assert
			.That(batch.Steps.Select(step => step.Event))
			.AreOfUnionType<IEnumerable<TraceEvent>, TraceEvent>(
				typeof(TitleStarted),
				typeof(HopAsked),
				typeof(HopAnswered),
				typeof(TitleSettled),
				typeof(TitleStarted),
				typeof(TitleSettled)
			);
		await Assert.That((await SnapshotAsync(testee)).Nodes.Single().Answered).IsEqualTo(2);
	}

	[Test]
	public async Task Read_ForAnotherOwner_ReturnsNoTrace()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using var run = testee.Start(_feed, Owner);

		var read = testee.Read(_feed, UserId.From(Guid.NewGuid()), TraceCursor.Start);

		await Assert.That(read).IsOfUnionType<TraceRead, NoTrace>();
	}

	[Test]
	public async Task Start_WithinTheRetention_GivesANewRunKey()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Answered);
		}

		testee.End(_feed, new FeedRunFailed("The source returned nothing."));
		var first = await SnapshotAsync(testee);
		using var again = testee.Start(_feed, Owner);

		await Assert.That(first.Ended).IsSome();
		var second = await SnapshotAsync(testee);
		await Assert.That(second.Cursor.Run).IsNotEqualTo(first.Cursor.Run);
		await Assert.That(second.Ended).IsNone();
	}

	[Test]
	public async Task Hop_WithA429ThenAnAnswer_RecordsAfterRateLimit()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Answered, 429, 200);
		}

		await Assert.That(await AnswersAsync(testee)).IsEquivalentTo([HopOutcome.AfterRateLimit]);
	}

	[Test]
	public async Task Hop_EndingOnA429_RecordsGaveUp()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Failed, 429, 429);
		}

		await Assert.That(await AnswersAsync(testee)).IsEquivalentTo([HopOutcome.GaveUp]);
	}

	[Test]
	public async Task Hop_TimingOutAfterA429_RecordsGaveUp()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Failed, 429, 0);
		}

		await Assert.That(await AnswersAsync(testee)).IsEquivalentTo([HopOutcome.GaveUp]);
	}

	[Test]
	public async Task GaveUp_TwiceForOneService_RecordsItOnce()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			var title = FeedRunSpans.Title("Heat", new None());
			using (FeedRunSpans.Enter(title))
			{
				FeedRunSpans.GaveUp(RemoteService.Plex, TraceStage.Ids);
				FeedRunSpans.GaveUp(RemoteService.Plex, TraceStage.Ids);
			}
		}

		await Assert.That((await SnapshotAsync(testee)).Nodes.Single().GaveUp).IsEqualTo(1);
	}

	[Test]
	public async Task Stopped_WithAMissingTag_DoesNotThrow()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			var title = FeedRunSpans.Title("Heat", new None());
			using (FeedRunSpans.Enter(title))
			{
				Untagged.StartActivity(FeedRunNames.Hop)?.Stop();
				Untagged.StartActivity(FeedRunNames.Wait)?.Stop();
			}

			Untagged.StartActivity(FeedRunNames.Source)?.Stop();
		}

		var snapshot = await SnapshotAsync(testee);
		await Assert.That(snapshot.Nodes).IsEmpty();
		await Assert.That(snapshot.Requests).IsNone();
	}

	[Test]
	public async Task End_WithTitlesStillInFlight_ClearsThem()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			using (FeedRunSpans.Enter(FeedRunSpans.Title("Heat", new None())))
			{
				_ = FeedRunSpans.Hop(RemoteService.Plex, TraceStage.Ids);
			}
		}

		testee.End(_feed, new FeedRunFailed("The source returned nothing."));

		var snapshot = await SnapshotAsync(testee);
		await Assert.That(snapshot.Titles).IsEmpty();
		await Assert.That(snapshot.Nodes.Single().Waiting).IsEqualTo(0);
		await Assert.That(snapshot.Requests).IsSome().And.IsEqualTo(1);
	}

	[Test]
	public async Task GaveUp_AfterAHopThatGaveUp_CountsOnce()
	{
		using var testee = new FeedRunTrace(NullLogger<FeedRunTrace>.Instance);
		using (testee.Start(_feed, Owner))
		{
			Resolve("Heat", HopOutcome.Failed, 429);
			using (FeedRunSpans.Enter(FeedRunSpans.Title("Ronin", new None())))
			{
				using (var hop = FeedRunSpans.Hop(RemoteService.Plex, TraceStage.Ids))
				{
					using (var call = Http.StartActivity("GET"))
					{
						call?.SetTag("http.response.status_code", 429);
					}

					hop.Answered(HopOutcome.Failed);
				}

				FeedRunSpans.GaveUp(RemoteService.Plex, TraceStage.Ids);
			}
		}

		await Assert.That((await SnapshotAsync(testee)).Nodes.Single().GaveUp).IsEqualTo(2);
	}

	private static void Resolve(string name, HopOutcome answer, params int[] statuses)
	{
		var title = FeedRunSpans.Title(name, new None());
		using (FeedRunSpans.Enter(title))
		{
			using (var hop = FeedRunSpans.Hop(RemoteService.Plex, TraceStage.Ids))
			{
				foreach (var status in statuses)
				{
					using var call = Http.StartActivity("GET");
					if (status > 0)
					{
						call?.SetTag("http.response.status_code", status);
					}
				}

				hop.Answered(answer);
			}

			FeedRunSpans.Settle(title, TitleFate.Matched);
		}
	}

	private async Task<IReadOnlyList<HopOutcome>> AnswersAsync(FeedRunTrace testee)
	{
		var batch = await BatchAsync(testee, new TraceCursor((await SnapshotAsync(testee)).Cursor.Run, 0));
		return [.. batch.Steps.Select(Answer).WhereSome()];
	}

	private static Optional<HopOutcome> Answer(TraceStep step) =>
		step.Event switch
		{
			HopAnswered answered => Some.Of(answered.Outcome),
			_ => new None(),
		};

	private async Task<TraceSnapshot> SnapshotAsync(FeedRunTrace testee) =>
		(await Assert.That(testee.Read(_feed, Owner, TraceCursor.Start)).IsOfUnionType<TraceRead, TraceSnapshot>())!;

	private async Task<TraceBatch> BatchAsync(FeedRunTrace testee, TraceCursor cursor) =>
		(await Assert.That(testee.Read(_feed, Owner, cursor)).IsOfUnionType<TraceRead, TraceBatch>())!;
}
