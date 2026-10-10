using System.Diagnostics;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Processing;
using Domain.Providers;
using UI.Features.Feeds.Components;

namespace UI.Features.Settings.Components.Testing;

internal sealed class DemoLiveRun : ILiveRun, IDisposable
{
	private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);

	private const int Linger = 5000;

	private const int PageSize = 40;

	private static readonly (string Name, int Year)[] Names =
	[
		("Free Solo", 2018),
		("Man on Wire", 2008),
		("Searching for Sugar Man", 2012),
		("Won't You Be My Neighbor?", 2018),
		("Amy", 2015),
		("Senna", 2010),
		("The Act of Killing", 2012),
		("Hoop Dreams", 1994),
		("Grizzly Man", 2005),
		("Jiro Dreams of Sushi", 2011),
		("Blackfish", 2013),
		("Icarus", 2017),
		("My Octopus Teacher", 2020),
		("Apollo 11", 2019),
		("They Shall Not Grow Old", 2018),
		("Exit Through the Gift Shop", 2010),
		("Bowling for Columbine", 2002),
		("March of the Penguins", 2005),
		("Super Size Me", 2004),
		("An Inconvenient Truth", 2006),
		("The Fog of War", 2003),
		("Touching the Void", 2003),
		("Capturing the Friedmans", 2003),
		("Stories We Tell", 2012),
		("Citizenfour", 2014),
		("13th", 2016),
		("I Am Not Your Negro", 2016),
		("Honeyland", 2019),
		("Collective", 2019),
		("Fire of Love", 2022),
		("Navalny", 2022),
		("All the Beauty and the Bloodshed", 2022),
		("Summer of Soul (...Or, When the Revolution Could Not Be Televised)", 2021),
		("Flee", 2021),
		("The Rescue", 2021),
		("Minding the Gap", 2018),
		("Three Identical Strangers", 2018),
		("Shirkers", 2018),
		("Hale County This Morning, This Evening", 2018),
		("RBG", 2018),
		("Faces Places", 2017),
		("Jane", 2017),
		("Last Men in Aleppo", 2017),
		("Strong Island", 2017),
		("O.J.: Made in America", 2016),
		("Cameraperson", 2016),
		("Weiner", 2016),
		("Fire at Sea", 2016),
		("The Look of Silence", 2014),
		("Virunga", 2014),
		("Finding Vivian Maier", 2013),
		("20 Feet from Stardom", 2013),
		("The Square", 2013),
		("Cutie and the Boxer", 2013),
		("The Imposter", 2012),
		("How to Survive a Plague", 2012),
		("Undefeated", 2011),
		("Cave of Forgotten Dreams", 2010),
		("Inside Job", 2010),
		("Restrepo", 2010),
		("Waiting for \"Superman\"", 2010),
		("The Cove", 2009),
		("Food, Inc.", 2008),
		("Waltz with Bashir", 2008),
		("Anvil! The Story of Anvil", 2008),
		("The King of Kong: A Fistful of Quarters", 2007),
		("Taxi to the Dark Side", 2007),
		("Jesus Camp", 2006),
		("Murderball", 2005),
		("Enron: The Smartest Guys in the Room", 2005),
		("Born into Brothels", 2004),
		("Tarnation", 2003),
		("Spellbound", 2002),
		("Winged Migration", 2001),
		("Buena Vista Social Club", 1999),
		("American Movie", 1999),
		("Mr. Death", 1999),
		("The Celluloid Closet", 1995),
		("Crumb", 1994),
		("Baraka", 1992),
		("Hearts of Darkness: A Filmmaker's Apocalypse", 1991),
		("Paris Is Burning", 1990),
		("Roger & Me", 1989),
		("The Thin Blue Line", 1988),
		("Shoah", 1985),
		("Koyaanisqatsi", 1982),
		("Samsara", 2011),
		("Particle Fever", 2013),
		("The Biggest Little Farm", 2018),
		("Becoming", 2020),
		("Crip Camp", 2020),
		("Time", 2020),
		("Dick Johnson Is Dead", 2020),
		("The Social Dilemma", 2020),
		("American Factory", 2019),
		("For Sama", 2019),
		("The Cave", 2019),
		("Knock Down the House", 2019),
		("The Great Hack", 2019),
		("Fyre", 2019),
		("Rolling Thunder Revue", 2019),
		("Gunda", 2020),
		("The Truffle Hunters", 2020),
		("Beyond Utopia", 2023),
		("20 Days in Mariupol", 2023),
		("Four Daughters", 2023),
		("Bobi Wine: The People's President", 2022),
		("To Kill a Tiger", 2022),
		("The Eternal Memory", 2023),
		("Still: A Michael J. Fox Movie", 2023),
		("American Symphony", 2023),
		("No Other Land", 2024),
		("Porcelain War", 2024),
		("Sugarcane", 2024),
		("Black Box Diaries", 2024),
		("Soundtrack to a Coup d'Etat", 2024),
		("Daughters", 2024),
		("Will & Harper", 2024),
		("Moonage Daydream", 2022),
		("Good Night Oppy", 2022),
	];

	private readonly Lock _gate = new();
	private readonly Script _script = Script.Write(Names);
	private readonly Guid _run = Guid.NewGuid();
	private readonly long _started = Stopwatch.GetTimestamp();
	private readonly PeriodicTimer _timer = new(Tick);
	private int _sent;
	private FateTally _fates = FateTally.None;
	private Optional<int> _requests = new None();
	private Optional<FeedRunEnd> _ended = new None();

	private Optional<SourcePage> _page = new None();

	private readonly Dictionary<RemoteService, int> _waiting = [];

	private const int ReadingAfterMs = 900;

	private Optional<int> _lastStartMs = new None();

	private static readonly FeedRunCounts Counts = new FeedRunCounts
	{
		Total = 120,
		Added = 6,
		Kept = 44,
		Bypassed = 0,
		Filtered = 30,
		Skipped = 16,
		UnmatchedNotFound = 20,
		UnmatchedCouldNotLookUp = 4,
		Removed = 0,
		Known = 34,
		NotFoundEarlier = 10,
	};

	public DemoLiveRun()
	{
		_ = PlayAsync();
	}

	public Feed Feed { get; } = DemoFeeds.DocumentariesFeed with { Limit = Some.Of(new QueryLimit { Count = 50 }) };

	public RunLayout Layout { get; } =
		new RunLayout(
			[MediaProvider.Tmdb, MediaProvider.Tvdb],
			[
				new(RemoteService.Plex, 1, false),
				new(RemoteService.AnimeLists, 1, true),
				new(RemoteService.Mdblist, 2, false),
				new(RemoteService.Tmdb, 3, false),
				new(RemoteService.Wikidata, 4, false),
				new(RemoteService.Tvmaze, 4, false),
				new(RemoteService.Trakt, 5, false),
			],
			[
				RemoteService.Tmdb,
				RemoteService.Plex,
				RemoteService.Mdblist,
				RemoteService.Tvmaze,
				RemoteService.ImdbDatasets,
				RemoteService.AniList,
				RemoteService.Mal,
				RemoteService.Trakt,
			],
			[
				new(new None(), RemoteService.Plex),
				new(new None(), RemoteService.Mdblist),
				new(Some.Of(RemoteService.Plex), RemoteService.Mdblist),
				new(Some.Of(RemoteService.Plex), RemoteService.Tmdb),
				new(Some.Of(RemoteService.Mdblist), RemoteService.Tmdb),
				new(Some.Of(RemoteService.Tmdb), RemoteService.Wikidata),
				new(Some.Of(RemoteService.Tmdb), RemoteService.Tvmaze),
				new(Some.Of(RemoteService.Wikidata), RemoteService.Trakt),
				new(Some.Of(RemoteService.Tvmaze), RemoteService.Trakt),
			]
		)
		{
			Sink = SinkKind.Arr,
		};

	public RunTally Tally { get; private set; } = RunTally.Before(Some.Of(Names.Length));

	public bool Hidden { get; private set; }

	public Optional<Guid> TraceKey => Some.Of(_run);

	public bool HasFolded { get; private set; }

	public event Action<WireBatch>? Batch;

	public event Action? Folded;

	public Task<Optional<TraceSnapshot>> SnapshotAsync(CancellationToken cancellationToken = default)
	{
		lock (_gate)
		{
			return Task.FromResult<Optional<TraceSnapshot>>(Some.Of(Snapshot()));
		}
	}

	public void Hide() => Hidden = true;

	public void Show() => Hidden = false;

	public void Dispose() => _timer.Dispose();

	private int Elapsed => (int)Stopwatch.GetElapsedTime(_started).TotalMilliseconds;

	private async Task PlayAsync()
	{
		while (await _timer.WaitForNextTickAsync())
		{
			Batch?.Invoke(Deliver());
			if (Elapsed >= _script.EndMs + Linger)
			{
				_timer.Dispose();
				HasFolded = true;
				Folded?.Invoke();
			}
		}
	}

	private WireBatch Deliver()
	{
		lock (_gate)
		{
			var elapsed = Elapsed;
			var from = _sent;
			while (_sent < _script.Steps.Count && _script.Steps[_sent].AtMs <= elapsed)
			{
				Count(_script.Steps[_sent++]);
			}

			_ended = Optional.When(elapsed >= _script.EndMs, () => new FeedRunEnd(TraceEnding.Completed, _script.EndMs) { Counts = Some.Of(Counts) });
			var until = _script.Blocked.Where(window => window.From <= elapsed && elapsed < window.Until).Select(window => window.Until).DefaultIfEmpty(0).Max();
			var progress = Progress(elapsed);
			Tally = RunTally.Of(_fates, _requests, Math.Min(elapsed, _script.EndMs), _ended, Some.Of(Names.Length)) with
			{
				Progress = progress,
				RateLimited = progress.Busiest.Match(wait => wait.Service is RemoteService.Tmdb && until > elapsed, () => false),
			};
			return new WireBatch(
				[.. _script.Steps.Skip(from).Take(_sent - from).Select(LiveWire.Step)],
				[new WireCapacity((byte)RemoteService.Tmdb, until, 0)],
				_ended.IsSome()
			)
			{
				Reading = progress.Reading.ToNullable(),
			};
		}
	}

	private void Count(TraceStep step)
	{
		var traced = step.Event;
		if (traced is TitleStarted)
		{
			_lastStartMs = Some.Of(step.AtMs);
		}

		_fates = traced switch
		{
			TitleStarted => _fates with { Started = _fates.Started + 1 },
			TitleKnown => _fates with { Known = _fates.Known + 1 },
			TitleNotFoundEarlier => _fates with { Earlier = _fates.Earlier + 1 },
			ListedWithoutIds => _fates with { ListedWithoutIds = _fates.ListedWithoutIds + 1 },
			TitleSettled { Fate: TitleFate.Matched } => _fates with { Matched = _fates.Matched + 1 },
			TitleSettled { Fate: TitleFate.CouldNotLookUp } => _fates with { CouldNotLookUp = _fates.CouldNotLookUp + 1 },
			TitleSettled { Fate: TitleFate.NotFound } => _fates with { NotFound = _fates.NotFound + 1 },
			_ => _fates,
		};
		switch (traced)
		{
			case HopAsked asked:
				_waiting[asked.Service] = _waiting.GetValueOrDefault(asked.Service) + 1;
				break;
			case HopAnswered answered:
				_waiting[answered.Service] = _waiting.GetValueOrDefault(answered.Service) - 1;
				break;
		}
		if (traced is SourcePage page)
		{
			_page = Some.Of(page);
		}

		if (traced is SourceEnded ended)
		{
			_requests = Some.Of(ended.Requests);
		}
	}

	private TraceSnapshot Snapshot()
	{
		var flights = new Dictionary<int, TitleInFlight>();
		var nodes = new Dictionary<HopPlace, NodeTally>();
		var spent = new Dictionary<HopPlace, (long Ms, int Count)>();
		foreach (var step in _script.Steps.Take(_sent))
		{
			var title = step.Title.GetValueOr(-1);
			switch (step.Event)
			{
				case TitleStarted started:
					flights[title] = new TitleInFlight(title, started.Name, started.Year, new None(), new None(), new None());
					break;
				case HopAsked asked:
					var place = new HopPlace(asked.Service, asked.Stage);
					flights[title] = flights[title] with { At = Some.Of(place) };
					nodes[place] = Node(nodes, place) with { Waiting = Node(nodes, place).Waiting + 1 };
					break;
				case HopAnswered answered:
					var answeredAt = new HopPlace(answered.Service, answered.Stage);
					var node = Node(nodes, answeredAt);
					nodes[answeredAt] = answered.Outcome switch
					{
						HopOutcome.Answered => node with { Answered = node.Answered + 1, Waiting = node.Waiting - 1 },
						HopOutcome.AfterRateLimit => node with { AfterRateLimit = node.AfterRateLimit + 1, Waiting = node.Waiting - 1 },
						HopOutcome.GaveUp => node with { GaveUp = node.GaveUp + 1, Waiting = node.Waiting - 1 },
						HopOutcome.Failed => node with { Failed = node.Failed + 1, Waiting = node.Waiting - 1 },
					};
					var total = spent.GetValueOrDefault(answeredAt);
					spent[answeredAt] = (total.Ms + answered.Ms, total.Count + 1);
					flights[title] =
						answered.Stage is TraceStage.Ids
							? flights[title] with
							{
								At = new None(),
								LastIds = Some.Of(answered.Service),
							}
							: flights[title] with
							{
								At = new None(),
								LastDetails = Some.Of(answered.Service),
							};
					break;
				case GaveUp gaveUp:
					var gaveUpAt = new HopPlace(gaveUp.Service, gaveUp.Stage);
					nodes[gaveUpAt] = Node(nodes, gaveUpAt) with { GaveUp = Node(nodes, gaveUpAt).GaveUp + 1 };
					break;
				case TitleSettled:
					flights.Remove(title);
					break;
			}
		}

		int Mean(HopPlace place)
		{
			var (ms, count) = spent.GetValueOrDefault(place);
			return (int)(ms / Math.Max(1, count));
		}

		IReadOnlyList<NodeTally> tallies = [.. nodes.Values.Select(node => node with { MeanMs = Mean(node.Node) })];
		var cursor = new TraceCursor(Some.Of(_run), _sent);
		return new TraceSnapshot(cursor, Math.Min(Elapsed, _script.EndMs), [.. flights.Values], tallies, _fates, _requests, _ended)
		{
			Progress = Progress(Elapsed),
		};
	}

	private RunProgress Progress(int elapsed)
	{
		var reading = Optional.When(
			_ended.IsNone() && _requests.IsNone() && _lastStartMs.Match(at => elapsed - at > ReadingAfterMs, () => _fates.Started == 0),
			() => _page.Map(read => read.Page + 1).GetValueOr(1)
		);
		var busiest = _waiting
			.Where(pair => pair.Value > 0)
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key)
			.Select(pair => new ServiceWait(pair.Key, pair.Value))
			.FirstOrNone();
		return new RunProgress(_page, _waiting.Values.Sum()) { Busiest = busiest, Reading = reading };
	}

	private static NodeTally Node(Dictionary<HopPlace, NodeTally> nodes, HopPlace place)
	{
		return nodes.GetValueOrDefault(place, new NodeTally(place, 0, 0, 0, 0, 0, 0));
	}

	private sealed record Script(IReadOnlyList<TraceStep> Steps, int EndMs, IReadOnlyList<(int From, int Until)> Blocked)
	{
		internal static Script Write((string Name, int Year)[] names)
		{
			var writer = new Writer();
			for (var title = 0; title < names.Length; title++)
			{
				writer.Page(title, names.Length);
				writer.Title(title, names[title].Name, names[title].Year);
			}

			writer.Add(names.Length * 105 + 450, -1, new SourceEnded(names.Length));
			IReadOnlyList<TraceStep> steps =
			[
				.. writer
					.Events.OrderBy(entry => entry.At)
					.Select((entry, index) => new TraceStep(index + 1, entry.At, Optional.When(entry.Title >= 0, () => entry.Title), entry.Event)),
			];
			return new Script(steps, steps[^1].AtMs + 120, writer.Blocked);
		}
	}

	private sealed class Writer
	{
		private readonly Random _random = new(7);

		internal List<(int At, int Title, TraceEvent Event)> Events { get; } = [];

		internal List<(int From, int Until)> Blocked { get; } = [];

		internal void Add(int at, int title, TraceEvent traced) => Events.Add((at, title, traced));

		internal void Page(int title, int titles)
		{
			if (title % PageSize == 0)
			{
				Add(370 + title * 105, -1, new SourcePage(title / PageSize + 1, Some.Of((titles + PageSize - 1) / PageSize)));
			}
		}

		internal void Title(int title, string name, int year)
		{
			var at = 400 + title * 105 + _random.Next(60);
			Add(at, title, new TitleStarted(name, Some.Of(year)));
			var roll = _random.NextDouble();
			if (roll < 0.28)
			{
				Add(at + 6, title, new TitleKnown());
				Add(at + 8, title, new TitleSettled(TitleFate.Matched));
				return;
			}

			if (roll < 0.36)
			{
				Add(at + 6, title, new TitleNotFoundEarlier());
				Add(at + 8, title, new TitleSettled(TitleFate.NotFound));
				return;
			}

			Add(at + 4, title, new PathStarted());
			at = Hop(title, at + 10, RemoteService.Plex, TraceStage.Ids);
			var fate = _random.NextDouble();
			if (fate < 0.05)
			{
				Add(at + 900, title, new GaveUp(RemoteService.Tmdb, TraceStage.Ids));
				Add(at + 905, title, new TitleSettled(TitleFate.CouldNotLookUp));
				return;
			}

			at = Hop(title, at + 4, _random.NextDouble() < 0.2 ? RemoteService.Wikidata : RemoteService.Tmdb, TraceStage.Ids);
			if (fate < 0.15)
			{
				Add(at + 5, title, new TitleSettled(TitleFate.NotFound));
				return;
			}

			at = Hop(title, at + 4, RemoteService.Tmdb, TraceStage.Details);
			if (_random.NextDouble() < 0.4)
			{
				at = Hop(title, at + 4, RemoteService.Mdblist, TraceStage.Details);
			}

			Add(at + 5, title, new TitleSettled(TitleFate.Matched));
		}

		private int Hop(int title, int at, RemoteService service, TraceStage stage)
		{
			var limited = service is RemoteService.Tmdb && stage is TraceStage.Ids && _random.NextDouble() < 0.07;
			var ms = limited ? 1200 + _random.Next(600) : 60 + _random.Next(service is RemoteService.Plex ? 220 : 160);
			Add(at, title, new HopAsked(service, stage));
			Add(at + ms, title, new HopAnswered(service, stage, limited ? HopOutcome.AfterRateLimit : HopOutcome.Answered, ms));
			if (limited)
			{
				Blocked.Add((at + 100, at + ms));
			}

			return at + ms;
		}
	}
}
