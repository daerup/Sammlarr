using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Layout;
using UI.Shared.Services;

namespace UI.Tests.Layout;

public class PhoneMenuTests : BunitContext
{
	private readonly List<bool> _openChanges = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Menu_ForAMember_ListsHomeThePagesThenTheMembersSettings()
	{
		var cut = RenderMenu(UserRole.Member);

		var rows = cut.FindAll("nav a.menu-row").Select(row => row.TextContent.Trim());
		await Assert
			.That(rows)
			.IsEquivalentTo(
				["Home", "Browse", "Feeds", "Wantlist", "Blocklist", "Settings", "Providers", "Ratings", "Notifications", "Account", "About"],
				CollectionOrdering.Matching
			);
		await Assert.That(cut.Find("a.menu-sub[href='/settings?section=account']").TextContent.Trim()).IsEqualTo("Account");
		await Assert.That(cut.FindAll("[data-sheet][role=dialog]")).HasSingleItem();
	}

	[Test]
	[Arguments("/settings?section=notifications", "Notifications")]
	[Arguments("/settings", "Providers")]
	public async Task Section_OnTheSettingsPage_IsTheOnlyCurrentRow(string at, string shown)
	{
		var cut = RenderMenu(UserRole.Member, at);

		var current = cut.FindAll("nav a[aria-current=page]").Select(row => row.TextContent.Trim());
		await Assert.That(current).IsEquivalentTo([shown]);
		await Assert.That(cut.FindAll("nav a.on")).HasSingleItem();
	}

	[Test]
	public async Task Version_WhenANewerReleaseIsOut_CarriesADotAndSaysSo()
	{
		var update = Some.Of(new AppUpdate("1.5.0", new Uri("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0")));

		var cut = RenderMenu(UserRole.Member, "/", update);

		await Assert.That(cut.FindAll("[data-testid=menu-update-dot]")).HasSingleItem();
		await Assert.That(cut.Find("a.menu-version").GetAttribute("title")).IsEqualTo("Sammlarr 1.5.0 is out");
	}

	[Test]
	public async Task DarkMode_WhenSwitchedOn_DarkensTheTheme()
	{
		var cut = RenderMenu(UserRole.Member);

		await cut.Find("button[role=switch][aria-label='Dark mode']").ClickAsync();

		await Assert.That(Services.GetRequiredService<ThemeService>().IsDarkMode).IsTrue();
	}

	[Test]
	public async Task DarkMode_WhenTheThemeChangesElsewhere_FollowsIt()
	{
		var cut = RenderMenu(UserRole.Member);

		await cut.InvokeAsync(() => Services.GetRequiredService<ThemeService>().SetDarkModeAsync(true));

		await Assert.That(cut.Find("button[role=switch][aria-label='Dark mode']").GetAttribute("aria-checked")).IsEqualTo("true");
	}

	[Test]
	public async Task Navigation_WhileOpen_ClosesTheMenu()
	{
		var cut = RenderMenu(UserRole.Admin);

		await cut.InvokeAsync(() => Services.GetRequiredService<NavigationManager>().NavigateTo("/feeds"));

		cut.WaitForState(() => _openChanges.Count > 0);
		await Assert.That(_openChanges).IsEquivalentTo([false]);
	}

	private IRenderedComponent<IComponent> RenderMenu(UserRole role, string at = "/") => RenderMenu(role, at, new None());

	private IRenderedComponent<IComponent> RenderMenu(UserRole role, string at, Optional<AppUpdate> update)
	{
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { Role = role });
		Services.GetRequiredService<NavigationManager>().NavigateTo(at);
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<PhoneMenu>(1);
			builder.AddAttribute(2, nameof(PhoneMenu.Open), true);
			builder.AddAttribute(3, nameof(PhoneMenu.OpenChanged), EventCallback.Factory.Create<bool>(this, _openChanges.Add));
			builder.AddAttribute(4, nameof(PhoneMenu.Version), new AppVersion("1.4.0"));
			builder.AddAttribute(5, nameof(PhoneMenu.Update), update);
			builder.CloseComponent();
		});
	}
}
