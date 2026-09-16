using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaIdentityTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheTmdbIdThroughPlex()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheImdbIdThroughPlex()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Imdb, null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Imdb("tt0137523"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheTvdbIdThroughPlex()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Game of Thrones", Tvdb("81189"), Plex("1396")));

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tvdb, null, Plex("1396"))]).ToListAsync();

		await AssertResolved(results[0], Tvdb("81189"));
	}

	[Test]
	public async Task IdentifyAsync_WhenPlexCannotSupplyTheProvider_ResolvesToNothing()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Plex("550")));

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tvdb, null, Plex("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenTheProviderErrors_LeavesTheIdUnresolved()
	{
		await host.EnsurePlexAccount();
		host.PlexLookupFails(Spec("Mystery", Plex("550")));

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenResolvingAPlexId_StoresEveryIdPlexReported()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenATmdbIdAndTitle_ResolvesThePlexIdThroughSearch()
	{
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Plex, "Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenNoProviderCanPlaceTheId_ResolvesToNothing()
	{
		await host.PlexFindsNothing("Unknown Movie");

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Plex, "Unknown Movie", Tmdb("999"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenTheIdIsAlreadyKnown_TranslatesFromCacheWithoutContactingPlex()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenTwoIdsDescribeOneTitle_ResolveToASingleMedia()
	{
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550")), Request(MediaProvider.Plex, "Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenANewIdSharesAnExistingOne_FoldsIntoThatMedia()
	{
		await host.Movie(Spec("Fight Club", Tmdb("550"))).SeedAsync();
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club Plex", Tmdb("550"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
		await AssertStored(Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenTwoUnknownIdsResolveAlike_CollapseIntoOneMedia()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);
		host.PlexHolds(Spec("Fight Club Dup", Tmdb("550"), Plex("999")));

		await Testee.IdentifyAsync([Request(MediaProvider.Tmdb, null, Plex("550")), Request(MediaProvider.Tmdb, null, Plex("999"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"), Plex("999"));
	}

	[Test]
	public async Task IdentifyAsync_WhenAResultBridgesTwoMediaRows_IsRejectedAsAConflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club (Plex)", Plex("550")));
		host.PlexHolds(Spec("Fight Club Bridge", Tmdb("550"), Plex("550"), Imdb("tt0137523")));

		await Assert
			.That(async () => await Testee.IdentifyAsync([Request(MediaProvider.Plex, "Fight Club", Imdb("tt0137523"))]).ToListAsync())
			.Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task IdentifyAsync_WhenCandidatesAreSplitAcrossRows_AreRejectedAsAConflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();

		await Assert
			.That(async () => await Testee.IdentifyAsync([Request(MediaProvider.Plex, null, Tmdb("550"), Plex("550"))]).ToListAsync())
			.Throws<MediaIdentityConflictException>();
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

	private static async Task AssertResolved(Result<MediaIdentity> result, ProviderId expected) =>
		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async identity =>
			{
				var external = identity.Externals.OfProvider(expected.Provider);
				await Assert.That(external).IsNotNull();
				await Assert.That(external!.Value).IsEqualTo(expected.Value);
			});

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
