using Contracts.Media;
using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SinkAddressingTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Before(Test)]
	public async Task Background()
	{
		await host.EnsurePlexAccount();
		host.SinkAddresses(TestAddressing.ArrLike);
	}

	[Test]
	public async Task IdentifyAsync_ForASinkThatAddressesEachTypeByItsOwnId_ResolvesEachTitleToItsAddress()
	{
		host.PlexHolds(TestMedia.FightClub);
		host.PlexHolds(new MediaSpec("Breaking Bad", MediaType.Show, [Tvdb("81189"), Plex("1396")]));

		var results = await Testee.IdentifyAsync([Request(MediaType.Movie, Plex("550")), Request(MediaType.Show, Plex("1396"))]).ToListAsync();

		await Assert
			.That(results[0])
			.IsSuccess()
			.And.Multiple(async movie => await Assert.That(movie.Externals.OfProvider(MediaProvider.Tmdb).Map(id => id.Value)).IsSome().And.IsEqualTo("550"));
		await Assert
			.That(results[1])
			.IsSuccess()
			.And.Multiple(async show => await Assert.That(show.Externals.OfProvider(MediaProvider.Tvdb).Map(id => id.Value)).IsSome().And.IsEqualTo("81189"));
	}

	[Test]
	public async Task IdentifyAsync_ForATitleTheSinkCannotAddress_LeavesItUnmatched()
	{
		host.PlexHolds(new MediaSpec("Mystery", MediaType.Unknown, [Tmdb("42"), Plex("42")]));

		var results = await Testee.IdentifyAsync([Request(MediaType.Unknown, Plex("42"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task GetSinkIdsAsync_ForTitlesThatCarryEveryId_ReturnsEachTitlesOwnAddressAndSkipsATypeItCannotAddress()
	{
		var movie = await host.Movie(new MediaSpec("The Matrix", MediaType.Movie, [Tmdb("603"), Tvdb("9603"), Plex("603")])).SeedAsync();
		var show = await host.Show(new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Tvdb("81189"), Plex("1396")])).SeedAsync();
		var unknown = await host.Movie(new MediaSpec("Mystery", MediaType.Unknown, [Tmdb("42"), Tvdb("42"), Plex("42")])).SeedAsync();

		var sinkIds = await Testee.GetSinkIdsAsync([movie, show, unknown]);

		await Assert.That(sinkIds[movie]).IsEqualTo(TmdbId.From("603", MediaType.Movie));
		await Assert.That(sinkIds[show]).IsEqualTo(TvdbId.From("81189", MediaType.Show));
		await Assert.That(sinkIds).DoesNotContainKey(unknown);
	}

	[Test]
	public async Task EnsureSinkIdsAsync_ForATitleThatOnlyCarriesItsPlexId_FindsTheIdTheSinkAddsBy()
	{
		host.PlexHolds(TestMedia.FightClub);
		var movie = await host.Movie(new MediaSpec("Fight Club", MediaType.Movie, [Plex("550")])).SeedAsync();

		var missing = await Testee.EnsureSinkIdsAsync([movie], SinkKind.Plex);

		await Assert.That(missing).IsEmpty();
		await Assert.That((await Testee.GetSinkIdsAsync([movie]))[movie]).IsEqualTo(TmdbId.From("550", MediaType.Movie));
	}

	[Test]
	public async Task EnsureSinkIdsAsync_ForATitleNoProviderCanConvert_ReturnsIt()
	{
		var lost = new MediaSpec("Lost", MediaType.Movie, [Plex("999")]);
		host.PlexHasNoMetadataFor(lost);
		var movie = await host.Movie(lost).SeedAsync();

		await Assert.That(await Testee.EnsureSinkIdsAsync([movie], SinkKind.Plex)).IsEquivalentTo([movie]);
	}

	[Test]
	public async Task AddAsync_ForAShowWithoutTheIdTheSinkAddressesShowsBy_RefusesIt()
	{
		var breakingBad = new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Plex("1396")]);
		host.PlexHasNoMetadataFor(breakingBad);
		var show = await host.Show(breakingBad).SeedAsync();

		await Assert
			.That(async () => await host.Resolve<IWantlistService>().AddAsync([show], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search))))
			.Throws<MissingSinkIdException>();
	}

	[Test]
	public async Task AddAsync_ForAShowFoundOnlyByItsTmdbId_LooksUpTheIdTheSinkAddsShowsBy()
	{
		await host.TmdbKnowsTheExternalIdsOf(new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Tvdb("81189")]));
		var show = await host.Show(new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396")])).SeedAsync();

		await host.Resolve<IWantlistService>().AddAsync([show], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		await Assert.That((await Testee.GetSinkIdsAsync([show]))[show]).IsEqualTo(TvdbId.From("81189", MediaType.Show));
	}

	private static IdentifyRequest Request(MediaType type, ProviderId id) => new([id.ToExternal(type)], new None());
}
