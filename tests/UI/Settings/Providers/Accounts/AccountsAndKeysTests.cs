using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Domain.Providers;
using UI.Features.Settings.Components.Providers.Accounts;

namespace UI.Tests.Settings.Providers.Accounts;

public class AccountsAndKeysTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		ComponentFactories.AddStub<ApiKeyRow>();
		ComponentFactories.AddStub<AccountRow>();
		ComponentFactories.AddStub<SammlarrAccountRow>();
	}

	[Test]
	public async Task Rows_OnAPlexInstall_OfferFourKeysWithTmdbsTokenAndSetupStepsForMalAndTrakt()
	{
		var cut = Render<AccountsAndKeys>(parameters => parameters.Add(section => section.Config, ProviderBranding.OnPlexSearchingPlex));

		var rows = cut.FindComponents<Stub<ApiKeyRow>>().Select(row => row.Instance.Parameters).ToList();

		await Assert
			.That(rows.Select(row => row.Get(key => key.Provider)))
			.IsEquivalentTo([ApiKeyProvider.Tmdb, ApiKeyProvider.Mdblist, ApiKeyProvider.Mal, ApiKeyProvider.Trakt]);
		await Assert
			.That(rows.Where(row => row.ContainsKey(nameof(ApiKeyRow.Extra))).Select(row => row.Get(key => key.Provider)))
			.IsEquivalentTo([ApiKeyProvider.Tmdb]);
		await Assert
			.That(rows.Where(row => row.ContainsKey(nameof(ApiKeyRow.Setup))).Select(row => row.Get(key => key.Provider)))
			.IsEquivalentTo([ApiKeyProvider.Mal, ApiKeyProvider.Trakt]);
	}

	[Test]
	public async Task MalRow_SuggestsAnAppDescriptionLongEnoughForMyAnimeListsForm()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
		var cut = Render<AccountsAndKeys>(parameters => parameters.Add(section => section.Config, ProviderBranding.OnPlexSearchingPlex));
		var mal = cut.FindComponents<Stub<ApiKeyRow>>().Select(row => row.Instance.Parameters).Single(row => row.Get(key => key.Provider) == ApiKeyProvider.Mal);

		var setup = Render(mal.Get(key => key.Setup) ?? (_ => { }));
		await setup.Find("[data-key-setup] button").ClickAsync(new());

		var description = setup.FindComponents<SetupValue>().Single(value => value.Instance.Name == "App Description").Instance.Value;
		await Assert.That(description.Length).IsGreaterThanOrEqualTo(50);
	}
}
