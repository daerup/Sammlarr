using Bunit;
using Bunit.TestDoubles;
using Domain.Providers;
using Domain.Settings;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class AccountsAndKeysTests : BunitContext
{
	private static readonly ProviderConfig Config = new()
	{
		Sink = SinkKind.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	[Before(Test)]
	public void Setup()
	{
		ComponentFactories.AddStub<ApiKeyRow>();
		ComponentFactories.AddStub<AccountRow>();
		ComponentFactories.AddStub<SammlarrAccountRow>();
	}

	[Test]
	public async Task MdblistRow_SaysItFollowsMdblistListsAndTheListsMdblistMirrors()
	{
		var cut = Render<AccountsAndKeys>(parameters => parameters.Add(section => section.Config, Config));

		var mdblist = cut.FindComponents<Stub<ApiKeyRow>>()
			.Select(row => row.Instance.Parameters)
			.Single(row => row.Get(key => key.Provider) == ApiKeyProvider.Mdblist);

		await Assert
			.That(mdblist.Get(key => key.Uses))
			.IsEquivalentTo([
				"MDBList lists as feeds, like hdlists/latest-hd-documentary-movies-1980-to-today",
				"IMDb, Trakt and Letterboxd lists, once you add them on mdblist.com (a free account adds one, updated weekly)",
			]);
	}

	[Test]
	public async Task TmdbRow_OffersItsListsAsFeeds()
	{
		var cut = Render<AccountsAndKeys>(parameters => parameters.Add(section => section.Config, Config));

		var tmdb = cut.FindComponents<Stub<ApiKeyRow>>().Select(row => row.Instance.Parameters).Single(row => row.Get(key => key.Provider) == ApiKeyProvider.Tmdb);

		await Assert.That(tmdb.Get(key => key.Uses)).Contains(use => use.Contains("feeds"));
	}

	[Test]
	public async Task TmdbRow_CarriesTheOptionalReadTokenFieldOnlyForTmdb()
	{
		var cut = Render<AccountsAndKeys>(parameters => parameters.Add(section => section.Config, Config));

		var rows = cut.FindComponents<Stub<ApiKeyRow>>().Select(row => row.Instance.Parameters).ToList();

		await Assert
			.That(rows.Where(row => row.ContainsKey(nameof(ApiKeyRow.Extra))).Select(row => row.Get(key => key.Provider)))
			.IsEquivalentTo([ApiKeyProvider.Tmdb]);
	}
}
