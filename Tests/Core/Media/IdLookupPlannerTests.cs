using Contracts.Media.Conversions;
using Contracts.Providers;
using Core.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

public class IdLookupPlannerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderLoad Idle = new(new None(), 0);
	private static readonly ProviderLoad Blocked = new(Some.Of(Now.AddMinutes(1)), 0);
	private static readonly IProviderCapacity Open = TestCapacity.Open();

	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];
	private static readonly IdLookup PlexId = new ById(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);
	private static readonly IdLookup Search = new ByHints(Yields: Guids, Cost: 4);
	private static readonly IdLookup Detail = new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);
	private static readonly IdLookup Find = new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1);
	private static readonly IReadOnlyList<Hop> Hops = IdLookupPlanner.Derive([
		Converter(MediaProvider.Plex, [PlexId, Search]),
		Converter(MediaProvider.Tmdb, [Detail, Find]),
	]);

	[Test]
	public async Task Plan_TakesASingleStepWhenOneLookupYieldsTheTarget()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Tmdb, Hops, Open, Now);

		await Assert.That(Describe(plan.Paths[0])).IsEqualTo("find(Imdb)>Tmdb");
	}

	[Test]
	public async Task Plan_ChainsLookupsWhenNoSingleOneReachesTheTarget()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Tvdb, Hops, Open, Now);

		await Assert.That(Describe(plan.Paths[0])).IsEqualTo("find(Imdb)>Tmdb detail(Tmdb)>Tvdb");
	}

	[Test]
	public async Task Plan_StartsASearchOnlyFromTheRequestsHintsOrAfterAHintsStep()
	{
		var fromHints = IdLookupPlanner.Plan(new HashSet<MediaProvider>(), true, MediaProvider.Plex, Hops, Open, Now);
		var withoutHints = IdLookupPlanner.Plan(new HashSet<MediaProvider>(), false, MediaProvider.Plex, Hops, Open, Now);
		var afterAHintsStep = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Plex, Hops, Open, Now);

		await Assert.That(fromHints.Paths.Select(Describe)).IsEquivalentTo(["search>Plex"]);
		await Assert.That(withoutHints.Paths).IsEmpty();
		await Assert
			.That(afterAHintsStep.Paths.Select(Describe))
			.IsEquivalentTo(["find(Imdb)>hints search>Plex", "find(Imdb)>Tmdb detail(Tmdb)>hints search>Plex"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_SkipsTheSearchOncePlexIsReached()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Plex }, true, MediaProvider.Tvdb, Hops, Open, Now);

		await Assert.That(plan.Paths.Select(Describe)).All().Satisfy(path => path.DoesNotContain("search"));
	}

	[Test]
	public async Task Plan_DoesNotLookUpAPlexIdRightAfterSearchingPlex()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider>(), true, MediaProvider.Tvdb, Hops, Open, Now);

		await Assert
			.That(plan.Paths.Select(Describe))
			.IsEquivalentTo(["search>Tvdb", "search>Tmdb detail(Tmdb)>Tvdb", "search>Imdb find(Imdb)>Tmdb detail(Tmdb)>Tvdb"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_NeverTakesAHopToAnIdTheTitleAlreadyHas()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tmdb, MediaProvider.Imdb }, true, MediaProvider.Tvdb, Hops, Open, Now);

		var reachedIds = plan.Paths.SelectMany(path => path).Select(Describe).Select(hop => hop[(hop.IndexOf('>') + 1)..]);
		await Assert.That(reachedIds).DoesNotContain(nameof(MediaProvider.Tmdb)).And.DoesNotContain(nameof(MediaProvider.Imdb));
	}

	[Test]
	public async Task Plan_ForATitleThatAlreadyHoldsTheTarget_RefreshesOnlyThroughPlex()
	{
		var plex = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Plex }, false, MediaProvider.Plex, Hops, Open, Now);
		var tmdb = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tmdb }, false, MediaProvider.Tmdb, Hops, Open, Now);
		var imdb = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Imdb, Hops, Open, Now);
		var tvdb = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tvdb }, false, MediaProvider.Tvdb, Hops, Open, Now);

		await Assert.That(plex.Paths.Select(Describe)).IsEquivalentTo(["plex id(Plex)>Plex"]);
		await Assert.That(tmdb.Paths.Select(Describe)).IsEquivalentTo([""]);
		await Assert.That(imdb.Paths.Select(Describe)).IsEquivalentTo([""]);
		await Assert.That(tvdb.Paths.Select(Describe)).IsEquivalentTo([""]);
	}

	[Test]
	public async Task Plan_LeavesOutEveryPathThroughABlockedProviderAndSaysSo()
	{
		var plan = IdLookupPlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			true,
			MediaProvider.Tmdb,
			Hops,
			TestCapacity.With(provider => provider is MediaProvider.Plex ? Blocked : Idle),
			Now
		);
		var refresh = IdLookupPlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Plex },
			false,
			MediaProvider.Plex,
			Hops,
			TestCapacity.With(provider => provider is MediaProvider.Plex ? Blocked : Idle),
			Now
		);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(["find(Imdb)>Tmdb"]);
		await Assert.That(plan.Blocked).IsTrue();
		await Assert.That(refresh.Paths).IsEmpty();
		await Assert.That(refresh.Blocked).IsTrue();
	}

	[Test]
	public async Task Plan_OrdersPathsByCostThenByDerivationOrder()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tmdb, MediaProvider.Imdb }, true, MediaProvider.Plex, Hops, Open, Now);

		await Assert
			.That(plan.Paths.Select(Describe))
			.IsEquivalentTo(
				["search>Plex", "detail(Tmdb)>hints search>Plex", "find(Imdb)>hints search>Plex", "detail(Tmdb)>Tvdb find(Tvdb)>hints search>Plex"],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Plan_BreaksATieInCostWithTheShorterPath()
	{
		IdLookup imdbToPlex = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Plex], Cost: 1);
		IdLookup plexToTvdb = new ById(Accepts: [MediaProvider.Plex], Yields: [MediaProvider.Tvdb], Cost: 1);
		IdLookup imdbToTvdb = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tvdb], Cost: 2);
		var hops = IdLookupPlanner.Derive([Converter(MediaProvider.Mdblist, [imdbToPlex, plexToTvdb, imdbToTvdb])]);

		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Tvdb, hops, Open, Now);

		await Assert.That(plan.Paths.Select(path => path.Count)).IsEquivalentTo([1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_NeverTakesMoreThanThreeHops()
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tvdb }, false, MediaProvider.Plex, Hops, Open, Now);

		await Assert
			.That(plan.Paths.Select(Describe))
			.IsEquivalentTo(["find(Tvdb)>hints search>Plex", "find(Tvdb)>Tmdb detail(Tmdb)>hints search>Plex"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_PrefersAnIdleDetourWhenTheDirectLookupsProviderIsBusy()
	{
		var plan = IdLookupPlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Tmdb },
			true,
			MediaProvider.Tvdb,
			Hops,
			TestCapacity.With(provider => provider is MediaProvider.Tmdb ? Idle with { Busy = 9 } : Idle),
			Now
		);

		await Assert.That(Describe(plan.Paths[0])).IsEqualTo("search>Tvdb");
	}

	[Test]
	public async Task Derive_SplitsALookupInputByInputInProviderOrder()
	{
		await Assert
			.That(Hops.Select(Describe))
			.IsEquivalentTo(
				[
					"plex id(Plex)>Tmdb",
					"plex id(Plex)>Imdb",
					"plex id(Plex)>Tvdb",
					"plex id(Plex)>Plex",
					"plex id(Plex)>hints",
					"search>Tmdb",
					"search>Imdb",
					"search>Tvdb",
					"search>Plex",
					"detail(Tmdb)>Imdb",
					"detail(Tmdb)>Tvdb",
					"detail(Tmdb)>hints",
					"find(Imdb)>Tmdb",
					"find(Imdb)>hints",
					"find(Tvdb)>Tmdb",
					"find(Tvdb)>hints",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	[MethodDataSource(nameof(PlexTargetCases))]
	public async Task Plan_ForThePlexTarget_FindsTheseOrderedPaths(MediaProvider[] have, bool hints, string[] expected)
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider>(have), hints, MediaProvider.Plex, Hops, Open, Now);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(expected, CollectionOrdering.Matching);
	}

	[Test]
	[MethodDataSource(nameof(OtherTargetCases))]
	public async Task Plan_ForOtherTargets_FindsTheseOrderedPaths(MediaProvider have, MediaProvider target, string[] expected)
	{
		var plan = IdLookupPlanner.Plan(new HashSet<MediaProvider> { have }, true, target, Hops, Open, Now);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(expected, CollectionOrdering.Matching);
	}

	public static IEnumerable<Func<(MediaProvider[] Have, bool Hints, string[] Expected)>> PlexTargetCases()
	{
		(MediaProvider[] Have, string[] Paths)[] withoutHints =
		[
			([], []),
			(
				[MediaProvider.Tmdb],
				["detail(Tmdb)>hints search>Plex", "detail(Tmdb)>Imdb find(Imdb)>hints search>Plex", "detail(Tmdb)>Tvdb find(Tvdb)>hints search>Plex"]
			),
			([MediaProvider.Imdb], ["find(Imdb)>hints search>Plex", "find(Imdb)>Tmdb detail(Tmdb)>hints search>Plex"]),
			(
				[MediaProvider.Tmdb, MediaProvider.Imdb],
				["detail(Tmdb)>hints search>Plex", "find(Imdb)>hints search>Plex", "detail(Tmdb)>Tvdb find(Tvdb)>hints search>Plex"]
			),
			([MediaProvider.Tvdb], ["find(Tvdb)>hints search>Plex", "find(Tvdb)>Tmdb detail(Tmdb)>hints search>Plex"]),
			(
				[MediaProvider.Tmdb, MediaProvider.Tvdb],
				["detail(Tmdb)>hints search>Plex", "find(Tvdb)>hints search>Plex", "detail(Tmdb)>Imdb find(Imdb)>hints search>Plex"]
			),
			(
				[MediaProvider.Imdb, MediaProvider.Tvdb],
				[
					"find(Imdb)>hints search>Plex",
					"find(Tvdb)>hints search>Plex",
					"find(Imdb)>Tmdb detail(Tmdb)>hints search>Plex",
					"find(Tvdb)>Tmdb detail(Tmdb)>hints search>Plex",
				]
			),
			(
				[MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb],
				["detail(Tmdb)>hints search>Plex", "find(Imdb)>hints search>Plex", "find(Tvdb)>hints search>Plex"]
			),
		];
		foreach (var (have, paths) in withoutHints)
		{
			yield return () => (have, false, paths);
			yield return () => (have, true, ["search>Plex", .. paths]);
			yield return () => ([MediaProvider.Plex, .. have], false, ["plex id(Plex)>Plex"]);
			yield return () => ([MediaProvider.Plex, .. have], true, ["plex id(Plex)>Plex"]);
		}
	}

	public static IEnumerable<Func<(MediaProvider Have, MediaProvider Target, string[] Expected)>> OtherTargetCases()
	{
		yield return () =>
			(
				MediaProvider.Tmdb,
				MediaProvider.Tvdb,
				["detail(Tmdb)>Tvdb", "search>Tvdb", "detail(Tmdb)>hints search>Tvdb", "detail(Tmdb)>Imdb find(Imdb)>hints search>Tvdb"]
			);
		yield return () =>
			(
				MediaProvider.Tmdb,
				MediaProvider.Imdb,
				["detail(Tmdb)>Imdb", "search>Imdb", "detail(Tmdb)>hints search>Imdb", "detail(Tmdb)>Tvdb find(Tvdb)>hints search>Imdb"]
			);
		yield return () =>
			(
				MediaProvider.Imdb,
				MediaProvider.Tmdb,
				["find(Imdb)>Tmdb", "search>Tmdb", "search>Tvdb find(Tvdb)>Tmdb", "find(Imdb)>hints search>Tmdb", "find(Imdb)>hints search>Tvdb find(Tvdb)>Tmdb"]
			);
	}

	private static IExternalIdConverter Converter(MediaProvider provider, IReadOnlyList<IdLookup> lookups)
	{
		var converter = IExternalIdConverter.Mock();
		converter.Provider.Returns(provider);
		converter.Lookups.Returns(lookups);
		return converter.Object;
	}

	private static string NameOf(ById lookup)
	{
		if (lookup.Accepts.Contains(MediaProvider.Plex))
		{
			return "plex id";
		}

		return lookup.Accepts.Contains(MediaProvider.Tmdb) ? "detail" : "find";
	}

	private static string Describe(IReadOnlyList<Hop> path)
	{
		return string.Join(" ", path.Select(Describe));
	}

	private static string Describe(Hop hop)
	{
		return hop switch
		{
			IdToId id => $"{NameOf(id.Lookup)}({id.From})>{id.To}",
			IdToHints hints => $"{NameOf(hints.Lookup)}({hints.From})>hints",
			HintsToId search => $"search>{search.To}",
		};
	}
}
