using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Users;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class UsersSectionTests : BunitContext
{
	[Test]
	[Arguments(RegistrationMode.Open, "true")]
	[Arguments(RegistrationMode.Closed, "false")]
	public async Task Render_ShowsWhetherNewSignUpsAreAllowedFromTheProviderConfig(RegistrationMode mode, string allowed)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(ICurrentUser.Mock().Object);
		var users = IUserManagementService.Mock();
		users.GetAllAsync(Any()).Returns([]);
		Services.AddSingleton(users.Object);
		var config = IProviderConfigService.Mock();
		config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = mode,
				}
			);
		Services.AddSingleton(config.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;

		var cut = Render<UsersSection>();

		await Assert.That(cut.WaitForElement("[aria-label='Allow new sign-ups']").GetAttribute("aria-checked")).IsEqualTo(allowed);
	}
}
