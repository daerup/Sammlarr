using Contracts.Settings;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Media;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class ExternalIdsViewTests
{
	private static readonly Optional<string> Title = Some.Of("The Matrix");

	private static readonly TmdbId Tmdb = TmdbId.From("603", MediaType.Movie);

	private readonly Mock<IProviderConfigService> _providers = IProviderConfigService.Mock();

	private readonly Mock<IArrSinkConfigService> _arr = IArrSinkConfigService.Mock();

	private readonly TestCurrentUser _user = new() { Role = UserRole.Member };

	[Test]
	public async Task IdCards_ForTwoPlexIds_LinkEachToItsOwnPageWithItsValueAndLabel()
	{
		var cards = Testee().IdCards([PlexId.From("5d776", MediaType.Movie), PlexId.From("5d777", MediaType.Movie)], Title);

		await Assert
			.That(cards.Select(card => card.Page))
			.IsEquivalentTo(
				[
					"https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F5d776",
					"https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F5d777",
				],
				CollectionOrdering.Matching
			);
		await Assert.That(cards[0].Subtext).IsEqualTo("5d776");
		await Assert.That(cards[0].Label).IsEqualTo("View 5d776 on Plex (opens in new tab)");
	}

	[Test]
	public async Task IdCards_ForRatingSiteIds_LeaveThemToTheRatingRows()
	{
		var cards = Testee()
			.IdCards(
				[
					RottenTomatoesId.From("m/the_matrix", MediaType.Movie),
					MetacriticId.From("the-matrix", MediaType.Movie),
					LetterboxdId.From("the-matrix", MediaType.Movie),
					TraktId.From("481", MediaType.Movie),
					Tmdb,
				],
				Title
			);

		await Assert.That(cards.Select(card => card.Brand.DisplayName)).IsEquivalentTo(["The Movie Database"]);
	}

	[Test]
	public async Task IdCards_ForAnIdWithoutAPage_LeaveItOut()
	{
		await Assert.That(Testee().IdCards([TvdbId.From("81189", MediaType.Unknown)], Title)).IsEmpty();
	}

	[Test]
	public async Task WithArrCardAsync_ForAMember_AddsNoCardAndReadsNoSettings()
	{
		var cards = await Testee().WithArrCardAsync(TmdbCards, [Tmdb]);

		await Assert.That(cards).Count().IsEqualTo(1);
		_providers.GetAsync(Any()).WasCalled(Times.Never);
		_arr.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task WithArrCardAsync_ForAnAdminOnAPlexWantlist_AddsNoCard()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Plex);

		var cards = await Testee().WithArrCardAsync(TmdbCards, [Tmdb]);

		await Assert.That(cards.Select(card => card.Brand.DisplayName)).IsEquivalentTo(["The Movie Database"]);
		_arr.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task WithArrCardAsync_ForAnAdminOnARadarrAndSonarrWantlist_EndsWithTheRadarrCard()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);

		var cards = await Testee().WithArrCardAsync(TmdbCards, [Tmdb]);

		await Assert.That(cards).Count().IsEqualTo(2);
		await Assert
			.That(cards[^1])
			.IsEqualTo(
				new ExternalIdCard(
					new MediaProviderBrandingService().GetBrandingInfo(RemoteService.Radarr),
					"tmdb:603",
					"http://radarr:7878/add/new?term=tmdb%3A603",
					"Find tmdb:603 in Radarr (opens in new tab)"
				)
			);
	}

	[Test]
	public async Task WithArrCardAsync_AskedTwice_ReadsTheRadarrAndSonarrSettingsOnce()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);
		var testee = Testee();

		await testee.WithArrCardAsync(TmdbCards, [Tmdb]);
		await testee.WithArrCardAsync(TmdbCards, [Tmdb]);

		_arr.GetAsync(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task WithArrCardAsync_AfterTheWantlistMovesBackToPlex_DropsTheRadarrCard()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);
		var testee = Testee();
		await testee.WithArrCardAsync(TmdbCards, [Tmdb]);

		Syncing(SinkKind.Plex);
		var cards = await testee.WithArrCardAsync(TmdbCards, [Tmdb]);

		await Assert.That(cards.Select(card => card.Brand.DisplayName)).IsEquivalentTo(["The Movie Database"]);
	}

	private void Syncing(SinkKind sink)
	{
		_providers.GetAsync(Any()).Returns(ProviderBranding.OnPlex with { Sink = sink });
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty.With(ArrApp.Radarr, ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878") }));
	}

	private IReadOnlyList<ExternalIdCard> TmdbCards => Testee().IdCards([Tmdb], Title);

	private ExternalIdsView Testee() => new(ProviderBranding.Linkers, new MediaProviderBrandingService(), _user, _providers.Object, _arr.Object);
}
