using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using TestInfrastructure;
using UI.Features.Settings.Components.About;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.About;

public class DataSourcesTests
{
	[Test]
	public async Task Asked_IsEveryServiceThatIsNotAnAppInstance()
	{
		await Assert.That(DataSources.Asked).IsEquivalentTo(Enum.GetValues<RemoteService>().Where(remote => remote.Access is not AppInstance));
	}

	[Test]
	public async Task Row_OfAConverter_NamesWhereItFindsATitleAndByWhichId()
	{
		var testee = Testee(
			[
				Registrations.Converter(
					RemoteService.Tmdb,
					new ById(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1),
					new ById(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1)
				),
			],
			[]
		);

		await Assert
			.That(Tip(testee.Row(RemoteService.Tmdb), "Matching"))
			.IsEqualTo("Matching: finds the same title on IMDb and TVDB by its TMDB id, and on TMDB by its IMDb or TVDB id");
	}

	[Test]
	public async Task Row_OfAConverterAskedWithOneId_LeavesThatIdOutOfWhatItFinds()
	{
		var testee = Testee(
			[
				Registrations.Converter(
					RemoteService.Plex,
					new ById(Accepts: [MediaProvider.Plex], Yields: [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1)
				),
			],
			[]
		);

		await Assert.That(Tip(testee.Row(RemoteService.Plex), "Matching")).IsEqualTo("Matching: finds the same title on TMDB, IMDb and TVDB by its Plex id");
	}

	[Test]
	public async Task Row_OfAnEnricher_NamesTheDetailsItFillsInFieldOrder()
	{
		var testee = Testee([], [Registrations.Enricher(RemoteService.Tvmaze, MediaField.RuntimePerEpisode | MediaField.Title | MediaField.ReleaseDate)]);

		await Assert.That(Tip(testee.Row(RemoteService.Tvmaze), "Details")).IsEqualTo("Details: title, release date and episode runtime");
	}

	[Test]
	public async Task Row_OfARatingSupplier_NamesItsRatingsWithRottenTomatoesRegisteredMark()
	{
		await Assert
			.That(Tip(Testee([], []).Row(RemoteService.Plex), "Ratings"))
			.IsEqualTo("Ratings: TMDB, IMDb, Rotten Tomatoes® critics and Rotten Tomatoes® audience");
	}

	[Test]
	public async Task Row_OfARole_SaysWhatTheRoleIs()
	{
		await Assert.That(Tip(Testee([], []).Row(RemoteService.Plex), "Sign-in")).IsEqualTo("Sign-in: who can sign in");
	}

	[Test]
	public async Task Row_OfARatingSite_SaysWhichServicesItsRatingsArriveThrough()
	{
		var row = Testee([], []).Row(MediaProvider.RottenTomatoes);

		await Assert.That(row.Chips.Select(chip => chip.Name)).IsEquivalentTo(["Ratings"]);
		await Assert.That(row.Via).IsSome().And.IsEqualTo("through Plex and MDBList");
	}

	[Test]
	public async Task Row_OfTheTvdb_IsReachedThroughItsPages()
	{
		var row = Testee([], []).Row(MediaProvider.Tvdb);

		await Assert.That(Tip(row, "Pages")).IsEqualTo("Pages: a title links to its page on TVDB");
		await Assert.That(row.Via).IsSome().And.IsEqualTo("linked from a title; posters in Sonarr's search results");
	}

	private static DataSources Testee(
		IEnumerable<Keyed<RemoteService, IExternalIdConverter>> converters,
		IEnumerable<Keyed<RemoteService, IDetailsEnricher>> enrichers
	)
	{
		return new DataSources(converters, enrichers, new MediaProviderBrandingService());
	}

	private static string Tip(SourceRow row, string chip) => row.Chips.Single(candidate => candidate.Name == chip).Tip;
}
