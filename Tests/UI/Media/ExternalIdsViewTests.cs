using Contracts.Settings;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
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
	public async Task Cards_ForTwoPlexIds_LinkEachToItsOwnPageWithItsValueAndLabel()
	{
		var cards = await Testee().CardsAsync([PlexId.From("5d776", MediaType.Movie), PlexId.From("5d777", MediaType.Movie)], Title);

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
	public async Task Cards_ForRatingSiteIds_LeaveThemToTheRatingRows()
	{
		var cards = await Testee()
			.CardsAsync(
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
	public async Task Cards_ForAnIdWithoutAPage_LeaveItOut()
	{
		await Assert.That(await Testee().CardsAsync([TvdbId.From("81189", MediaType.Unknown)], Title)).IsEmpty();
	}

	[Test]
	public async Task Cards_ForAMember_AreTheIdCardsAndReadNoSettings()
	{
		var cards = await Testee().CardsAsync([Tmdb], Title);

		await Assert.That(cards).Count().IsEqualTo(1);
		_providers.GetAsync(Any()).WasCalled(Times.Never);
		_arr.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Cards_ForAnAdminOnAPlexWantlist_AreTheIdCardsOnly()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Plex);

		var cards = await Testee().CardsAsync([Tmdb], Title);

		await Assert.That(cards.Select(card => card.Brand.DisplayName)).IsEquivalentTo(["The Movie Database"]);
		_arr.GetAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Cards_ForAnAdminOnARadarrAndSonarrWantlist_EndWithTheRadarrCard()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);

		var cards = await Testee().CardsAsync([Tmdb], Title);

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
	public async Task Cards_AskedTwice_ReadTheRadarrAndSonarrSettingsOnce()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);
		var testee = Testee();

		await testee.CardsAsync([Tmdb], Title);
		await testee.CardsAsync([Tmdb], Title);

		_arr.GetAsync(Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Cards_AfterTheWantlistMovesBackToPlex_DropTheRadarrCard()
	{
		_user.Role = UserRole.Admin;
		Syncing(SinkKind.Arr);
		var testee = Testee();
		await testee.CardsAsync([Tmdb], Title);

		Syncing(SinkKind.Plex);
		var cards = await testee.CardsAsync([Tmdb], Title);

		await Assert.That(cards.Select(card => card.Brand.DisplayName)).IsEquivalentTo(["The Movie Database"]);
	}

	private void Syncing(SinkKind sink)
	{
		_providers
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = sink,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		_arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty.With(ArrApp.Radarr, ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878") }));
	}

	private ExternalIdsView Testee() => new(ProviderBranding.Linkers, new MediaProviderBrandingService(), _user, _providers.Object, _arr.Object);
}
