using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Providers;
using Observability.Usage;

namespace Observability.Feed;

public static class FeedRunSpans
{
	public const string SourceName = "Sammlarr.FeedRun";

	private static readonly ActivitySource Spans = new(SourceName);

	public static Optional<Activity> Run(FeedId feed, Guid run)
	{
		return Optional.Of(Start(FeedRunNames.Run, [new(UsageTags.Feed, feed.ToString("D", CultureInfo.InvariantCulture)), new(FeedRunTags.Run, run.ToString())]));
	}

	public static Optional<Activity> Title(string name, Optional<int> year)
	{
		List<KeyValuePair<string, object?>> tags = [new(FeedRunTags.Name, name)];
		year.Apply(found => tags.Add(new(FeedRunTags.Year, found)));
		return Detached(FeedRunNames.Title, tags);
	}

	public static IDisposable Enter(Optional<Activity> title)
	{
		return title.Match<Activity, IDisposable>(span => new Entered(span), () => new FeedRunSpan(new None()));
	}

	public static void Known() => Nearest(FeedRunNames.Title).Apply(span => span.SetTag(FeedRunTags.Known, true));

	public static void NotFoundEarlier() => Nearest(FeedRunNames.Title).Apply(span => span.SetTag(FeedRunTags.Earlier, true));

	public static void Settle(Optional<Activity> title, TitleFate fate) => title.Apply(span => span.SetTag(FeedRunTags.Fate, fate)).Apply(span => span.Stop());

	public static async IAsyncEnumerable<T> SourceAsync<T>(
		IAsyncEnumerable<T> source,
		Func<T, bool> requested,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		var span = Detached(FeedRunNames.Source, []);
		var taken = 0;
		try
		{
			await foreach (var item in source.WithCancellation(cancellationToken))
			{
				if (requested(item))
				{
					taken++;
				}

				yield return item;
			}
		}
		finally
		{
			span.Apply(found => found.SetTag(FeedRunTags.Requests, taken)).Apply(found => found.Stop());
		}
	}

	public static void ListedWithoutIds() => Nearest(FeedRunNames.Run).Apply(_ => Start(FeedRunNames.Listed, [])?.Stop());

	public static void SourcePage(int page, Optional<int> pages)
	{
		List<KeyValuePair<string, object?>> tags = [new(FeedRunTags.Page, page)];
		pages.Apply(total => tags.Add(new(FeedRunTags.Pages, total)));
		Nearest(FeedRunNames.Run).Apply(_ => Start(FeedRunNames.Page, tags)?.Stop());
	}

	public static async Task<T> ReadingAsync<T>(int page, Func<Task<T>> read)
	{
		using var reading = Within(FeedRunNames.Run, FeedRunNames.Reading, [new(FeedRunTags.Page, page)]);
		return await read();
	}

	public static IDisposable Path() => Within(FeedRunNames.Title, FeedRunNames.Path, []);

	public static FeedRunSpan Hop(RemoteService service, TraceStage stage) => Within(FeedRunNames.Title, FeedRunNames.Hop, Placed(service, stage));

	public static void FreeHop(RemoteService service)
	{
		using var span = Hop(service, TraceStage.Ids);
		span.Answered(HopOutcome.Answered);
	}

	public static void GaveUp(RemoteService service, TraceStage stage)
	{
		Nearest(FeedRunNames.Title).Apply(_ => Start(FeedRunNames.Wait, Placed(service, stage))?.Stop());
	}

	private static Optional<Activity> Detached(string name, IEnumerable<KeyValuePair<string, object?>> tags)
	{
		return Nearest(FeedRunNames.Run)
			.Map(_ =>
			{
				var outer = Activity.Current;
				var span = Start(name, tags);
				Activity.Current = outer;
				return Optional.Of(span);
			});
	}

	private static FeedRunSpan Within(string parent, string name, IEnumerable<KeyValuePair<string, object?>> tags)
	{
		return new FeedRunSpan(Nearest(parent).Map(_ => Optional.Of(Start(name, tags))));
	}

	private static Activity? Start(string name, IEnumerable<KeyValuePair<string, object?>> tags)
	{
		return Spans.StartActivity(name, ActivityKind.Internal, default(ActivityContext), tags);
	}

	private static List<KeyValuePair<string, object?>> Placed(RemoteService service, TraceStage stage)
	{
		return [new(UsageTags.Service, service), new(FeedRunTags.Stage, stage)];
	}

	private static Optional<Activity> Nearest(string name)
	{
		return Lineage(Activity.Current).FirstOrNone(span => !span.IsStopped && span.Source == Spans && span.OperationName == name);
	}

	private static IEnumerable<Activity> Lineage(Activity? span)
	{
		for (var each = span; each is not null; each = each.Parent)
		{
			yield return each;
		}
	}

	private sealed class Entered : IDisposable
	{
		private readonly Activity? _outer = Activity.Current;

		internal Entered(Activity span) => Activity.Current = span;

		public void Dispose() => Activity.Current = _outer;
	}
}
