using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;
using UI.Features.Settings.Components.Users;

namespace UI.Tests.Settings.Users;

public class YouCardTests : BunitContext
{
	private readonly Mock<IProviderConfigService> _config = IProviderConfigService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(new AccountsOptions());
		Services.AddSingleton(ILocalAccountService.Mock().Object);
		Services.AddSingleton(_config.Object);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { DisplayName = "member", Role = UserRole.Member });
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Card_OnASammlarrAccountsInstall_OpensThePasswordFormInPlace()
	{
		SignsInWith(AuthProvider.Local);
		var cut = Render<YouCard>();
		cut.WaitForState(() => cut.FindAll("[data-you]").Count == 1);

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Change password").ClickAsync();

		await Assert.That(cut.Find("[data-signs-in]").TextContent.Trim()).IsEqualTo("Member · signs in with a Sammlarr account");
		await Assert.That(cut.FindAll("[data-change-password]")).HasSingleItem();
	}

	[Test]
	public async Task Card_OnAPlexInstall_HasNoPasswordToChange()
	{
		SignsInWith(AuthProvider.Plex);
		var cut = Render<YouCard>();
		cut.WaitForState(() => cut.FindAll("[data-you]").Count == 1);

		await Assert.That(cut.Find("[data-signs-in]").TextContent.Trim()).IsEqualTo("Member · signs in with Plex");
		await Assert.That(cut.FindAll("button")).IsEmpty();
	}

	private void SignsInWith(AuthProvider signIn)
	{
		_config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Tmdb,
					AuthProvider = signIn,
					RegistrationMode = RegistrationMode.Open,
				}
			);
	}
}
