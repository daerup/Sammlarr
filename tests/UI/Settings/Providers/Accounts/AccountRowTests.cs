using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Contracts.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers.Accounts;

namespace UI.Tests.Settings.Providers.Accounts;

public class AccountRowTests : BunitContext
{
	private readonly Mock<ICredentialService> _credentials = ICredentialService.Mock();

	private readonly List<bool> _opened = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_credentials.Object);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { DisplayName = "Daerup" });
		JSInterop.Mode = JSRuntimeMode.Loose;
		_credentials
			.GetAsync(AuthProvider.Plex, Any())
			.Returns(new TestCredential(AuthProvider.Plex, Guid.NewGuid(), new Secret("plex-token"), DateTimeOffset.MaxValue));
	}

	[Test]
	public async Task Row_WhenPlexIsTheSignIn_SaysWhoIsSignedInAndOffersNoTokenField()
	{
		var cut = RenderRow(signsIn: true);

		cut.WaitForState(() => cut.FindAll("[data-signed-in]").Count == 1);
		await Assert.That(cut.Find("[data-signed-in]").TextContent.Words()).Contains("Signed in as Daerup");
		await Assert.That(cut.FindAll("input")).IsEmpty();
		await Assert.That(cut.FindAll("button").Select(button => button.TextContent.Trim())).Contains("Reconnect with Plex");
	}

	[Test]
	public async Task Row_WhenPlexIsOnlyTheWatchlistLink_KeepsTheTokenField()
	{
		var cut = RenderRow(signsIn: false);

		cut.WaitForState(() => cut.FindAll("input").Count == 1);
		await Assert.That(cut.FindAll("[data-signed-in]")).IsEmpty();
	}

	[Test]
	public async Task Row_AfterARefusedReconnect_ShowsWhyOpensAndDropsTheErrorFromTheAddress()
	{
		var navigation = Services.GetRequiredService<NavigationManager>();
		navigation.NavigateTo("/setup?linkError=Plex%20returned%20Mia%20instead%20of%20Daerup.");

		var cut = RenderRow(signsIn: true);

		cut.WaitForState(() => cut.FindAll("[data-link-error]").Count == 1);
		await Assert.That(cut.Find("[data-link-error]").TextContent.Trim()).IsEqualTo("Plex returned Mia instead of Daerup.");
		await Assert.That(_opened).IsEquivalentTo([true]);
		await Assert.That(navigation.Uri).IsEqualTo("http://localhost/setup");
	}

	private IRenderedComponent<AccountRow> RenderRow(bool signsIn)
	{
		return Render<AccountRow>(parameters =>
			parameters
				.Add(row => row.Provider, AuthProvider.Plex)
				.Add(row => row.SignsIn, signsIn)
				.Add(row => row.Open, true)
				.Add(row => row.OpenChanged, open => _opened.Add(open))
				.Add(row => row.ReturnUrl, "/setup")
		);
	}
}
