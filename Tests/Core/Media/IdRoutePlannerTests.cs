using Contracts.Media.Conversions;
using Core.Media;
using Domain.Providers;

namespace Core.Tests.Media;

public class IdRoutePlannerTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly IExternalIdConverter Converter = IExternalIdConverter.Mock().Object;
	private static readonly RouteStep ImdbToTmdb = Step(MediaProvider.Imdb, MediaProvider.Tmdb, 1, MediaProvider.Tmdb);
	private static readonly RouteStep ImdbToPlex = Step(MediaProvider.Imdb, MediaProvider.Plex, 5, MediaProvider.Tmdb, MediaProvider.Plex);
	private static readonly RouteStep PlexToTmdb = Step(MediaProvider.Plex, MediaProvider.Tmdb, 1, MediaProvider.Plex);
	private static readonly RouteStep TmdbToTvdb = Step(MediaProvider.Tmdb, MediaProvider.Tvdb, 1, MediaProvider.Tmdb);
	private static readonly RouteStep HintsToPlex = new(Converter, new IdRoute(new None(), MediaProvider.Plex, [MediaProvider.Plex], 4));
	private static readonly RouteStep TmdbIdentity = Step(MediaProvider.Tmdb, MediaProvider.Tmdb, 0);

	private static readonly IReadOnlyList<RouteStep> Steps = [ImdbToTmdb, ImdbToPlex, PlexToTmdb, TmdbToTvdb, HintsToPlex, TmdbIdentity];

	[Test]
	public async Task Plan_PutsTheCheapestPathFirstAndTheAlternativesAfterIt()
	{
		var plan = IdRoutePlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Imdb }, false, MediaProvider.Tmdb, Steps, _ => Idle, Now);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(["Imdb>Tmdb", "Imdb>Plex Plex>Tmdb"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_LeavesOutEveryPathThroughABlockedProviderAndSaysSo()
	{
		var plan = IdRoutePlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			false,
			MediaProvider.Tmdb,
			Steps,
			provider => provider is MediaProvider.Plex ? Blocked : Idle,
			Now
		);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(["Imdb>Tmdb"]);
		await Assert.That(plan.Blocked).IsTrue();
	}

	[Test]
	public async Task Plan_PrefersADetourWhenTheDirectRoutesProviderIsBusy()
	{
		IReadOnlyList<RouteStep> steps =
		[
			ImdbToTmdb,
			Step(MediaProvider.Imdb, MediaProvider.Tvdb, 1, MediaProvider.Tvdb),
			Step(MediaProvider.Tvdb, MediaProvider.Tmdb, 1, MediaProvider.Tvdb),
		];

		var plan = IdRoutePlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			false,
			MediaProvider.Tmdb,
			steps,
			provider => provider is MediaProvider.Tmdb ? Idle with { Busy = 9 } : Idle,
			Now
		);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(["Imdb>Tvdb Tvdb>Tmdb", "Imdb>Tmdb"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
	}

	[Test]
	public async Task Plan_StartsFromTheHintsOnlyWhenThereAreHints()
	{
		var withHints = IdRoutePlanner.Plan(new HashSet<MediaProvider>(), true, MediaProvider.Tvdb, Steps, _ => Idle, Now);
		var without = IdRoutePlanner.Plan(new HashSet<MediaProvider>(), false, MediaProvider.Tvdb, Steps, _ => Idle, Now);

		await Assert.That(withHints.Paths.Select(Describe)).IsEquivalentTo(["hints>Plex Plex>Tmdb Tmdb>Tvdb"]);
		await Assert.That(without.Paths).IsEmpty();
	}

	[Test]
	public async Task Plan_ForATitleThatAlreadyHasTheTarget_TakesTheIdentityRouteFirst()
	{
		var plan = IdRoutePlanner.Plan(new HashSet<MediaProvider> { MediaProvider.Tmdb }, false, MediaProvider.Tmdb, Steps, _ => Idle, Now);

		await Assert.That(plan.Paths.Select(Describe)).IsEquivalentTo(["Tmdb>Tmdb"]);
	}

	[Test]
	public async Task Plan_NeverTakesMoreThanThreeHops()
	{
		var plan = IdRoutePlanner.Plan(
			new HashSet<MediaProvider> { MediaProvider.Imdb },
			false,
			MediaProvider.Mdblist,
			[ImdbToPlex, PlexToTmdb, TmdbToTvdb, Step(MediaProvider.Tvdb, MediaProvider.Mdblist, 1, MediaProvider.Mdblist)],
			_ => Idle,
			Now
		);

		await Assert.That(plan.Paths).IsEmpty();
	}

	private static readonly ProviderLoad Idle = new(new None(), 0);
	private static readonly ProviderLoad Blocked = new(Some.Of(Now.AddMinutes(1)), 0);

	private static RouteStep Step(MediaProvider from, MediaProvider to, int calls, params MediaProvider[] via) =>
		new(Converter, new IdRoute(Some.Of(from), to, [.. via], calls));

	private static string Describe(IReadOnlyList<RouteStep> path) =>
		string.Join(" ", path.Select(step => $"{(step.Route.From is Some<MediaProvider>(var from) ? from.ToString() : "hints")}>{step.Route.To}"));
}
