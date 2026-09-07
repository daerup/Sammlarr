using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaIdentityTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaService Service => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task A_plex_id_resolves_to_the_titles_tmdb_id_through_a_plex_lookup()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		var identity = await Resolve(MediaProvider.Tmdb, Plex("550"));

		await AssertResolved(identity, Tmdb("550"));
	}

	[Test]
	public async Task A_plex_id_resolves_to_the_titles_imdb_id_through_a_plex_lookup()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Plex("550")));

		var identity = await Resolve(MediaProvider.Imdb, Plex("550"));

		await AssertResolved(identity, Imdb("tt0137523"));
	}

	[Test]
	public async Task A_plex_id_resolves_to_the_titles_tvdb_id_through_a_plex_lookup()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Game of Thrones", Tvdb("81189"), Plex("1396")));

		var identity = await Resolve(MediaProvider.Tvdb, Plex("1396"));

		await AssertResolved(identity, Tvdb("81189"));
	}

	[Test]
	public async Task Resolving_to_a_provider_the_plex_lookup_cannot_supply_resolves_to_nothing()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Plex("550")));

		var identity = await Resolve(MediaProvider.Tvdb, Plex("550"));

		await Assert.That(identity).IsNull();
	}

	[Test]
	public async Task A_provider_error_during_resolution_leaves_the_id_unresolved()
	{
		await host.EnsurePlexAccount();
		host.PlexLookupFails(Spec("Mystery", Plex("550")));

		var identity = await Resolve(MediaProvider.Tmdb, Plex("550"));

		await Assert.That(identity).IsNull();
	}

	[Test]
	public async Task Resolving_a_plex_id_stores_every_id_plex_reported()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		await Resolve(MediaProvider.Tmdb, Plex("550"));

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task A_tmdb_id_and_a_title_resolve_to_the_plex_id_through_a_plex_search()
	{
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		var identity = await Resolve(MediaProvider.Plex, Tmdb("550"), title: "Fight Club");

		await AssertResolved(identity, Plex("550"));
	}

	[Test]
	public async Task An_id_the_providers_cannot_place_resolves_to_nothing()
	{
		await host.PlexFindsNothing("Unknown Movie");

		var identity = await Resolve(MediaProvider.Plex, Tmdb("999"), title: "Unknown Movie");

		await Assert.That(identity).IsNull();
	}

	[Test]
	public async Task An_id_already_in_the_library_is_translated_from_the_cache_without_contacting_plex()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var identity = await Resolve(MediaProvider.Tmdb, Plex("550"));

		await AssertResolved(identity, Tmdb("550"));
	}

	[Test]
	public async Task A_plex_id_and_a_tmdb_id_of_one_title_resolve_to_a_single_media()
	{
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		await ResolveAll(Request(MediaProvider.Tmdb, null, Plex("550")), Request(MediaProvider.Plex, "Fight Club", Tmdb("550")));

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task Resolving_a_new_id_folds_into_the_existing_media_that_already_holds_a_shared_id()
	{
		await host.Movie(Spec("Fight Club", Tmdb("550"))).SeedAsync();
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club Plex", Tmdb("550"), Plex("550")));

		var identity = await Resolve(MediaProvider.Tmdb, Plex("550"));

		await AssertResolved(identity, Tmdb("550"));
		await AssertStored(Plex("550"));
	}

	[Test]
	public async Task Two_unknown_plex_ids_that_resolve_to_the_same_tmdb_id_collapse_into_one_media()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);
		host.PlexHolds(Spec("Fight Club Dup", Tmdb("550"), Plex("999")));

		await ResolveAll(Request(MediaProvider.Tmdb, null, Plex("550")), Request(MediaProvider.Tmdb, null, Plex("999")));

		await AssertStored(Tmdb("550"), Plex("550"), Plex("999"));
	}

	[Test]
	public async Task A_provider_result_bridging_two_separate_media_rows_is_rejected_as_a_conflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club (Plex)", Plex("550")));
		host.PlexHolds(Spec("Fight Club Bridge", Tmdb("550"), Plex("550"), Imdb("tt0137523")));

		await AssertConflict(Request(MediaProvider.Plex, "Fight Club", Imdb("tt0137523")));
	}

	[Test]
	public async Task Candidates_already_split_across_two_media_rows_are_rejected_as_a_conflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();

		await AssertConflict(Request(MediaProvider.Plex, null, Tmdb("550"), Plex("550")));
	}

	private static MediaSpec Spec(string title, params ProviderId[] ids) => new(title, MediaType.Movie, ids);

	private static IExternalMediaId Ext(ProviderId id) => id.ToExternal(MediaType.Movie);

	private static Optional<ResolutionHints> Hints(string? title) =>
		title is null
			? new None()
			: Some.Of(
				new ResolutionHints
				{
					Title = title,
					Year = 0,
					MediaType = MediaType.Movie,
				}
			);

	private static ResolveRequest Request(MediaProvider target, string? title, params ProviderId[] candidates) =>
		new([.. candidates.Select(Ext)], target, Hints(title));

	private async Task<MediaIdentity?> Resolve(MediaProvider target, ProviderId candidate, string? title = null)
	{
		var results = await Service.IdentifyAsync([Request(target, title, candidate)]).ToListAsync();
		return results[0] is Success<MediaIdentity>(var identity) ? identity : null;
	}

	private async Task ResolveAll(params ResolveRequest[] requests) => await Service.IdentifyAsync(requests).ToListAsync();

	private async Task AssertConflict(params ResolveRequest[] requests) =>
		await Assert.That(async () => await Service.IdentifyAsync(requests).ToListAsync()).Throws<MediaIdentityConflictException>();

	private static async Task AssertResolved(MediaIdentity? identity, ProviderId expected)
	{
		await Assert.That(identity).IsNotNull();
		var external = identity!.Value.Externals.OfProvider(expected.Provider);
		await Assert.That(external).IsNotNull();
		await Assert.That(external!.Value).IsEqualTo(expected.Value);
	}

	private async Task AssertStored(params ProviderId[] ids)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		foreach (var id in ids)
		{
			var exists = await host.ScopedDbContext.ExternalMediaIds.AnyAsync(entry => entry.Source == id.Provider && entry.Id == id.Value);
			await Assert.That(exists).IsTrue();
		}
	}
}
