using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Contracts;
using Contracts.Identity;
using Contracts.Updates;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using UI.Features.Onboarding.Components;
using UI.Shared.Identity;
using UI.Shared.Layout;
using UI.Shared.Media;
using UI.Shared.Notifications.WebUi;
using UI.Shared.Services;

namespace UI.Tests.Layout;

public class MainLayoutTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped(_ => new CurrentUserAccessor(new CurrentUserOverride()));
		var versions = new AppVersionProvider(new ReleaseOptions(), new AppVersion("0.0.0-alpha.0.584"));
		Services.AddSingleton(versions);
		Services.AddSingleton(new AppUpdateChecker(versions, IReleaseFeed.Mock().Object, IDateTimeProvider.Mock().Object));
		Services.AddScoped<SearchLauncher>();
		ComponentFactories.AddStub<GlobalSearch>();
		ComponentFactories.AddStub<ProfileCard>();
		ComponentFactories.AddStub<SessionInvalidationListener>();
		ComponentFactories.AddStub<WebUiToastListener>();
		ComponentFactories.AddStub<SetupRedirect>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Search_WhenSignedOut_IsNotMounted()
	{
		AddAuthorization().SetNotAuthorized();

		var cut = RenderLayout();

		await Assert.That(cut.FindComponents<Stub<GlobalSearch>>()).IsEmpty();
		await Assert.That(cut.FindAll(".search-trigger")).IsEmpty();
	}

	[Test]
	public async Task Search_WhenSignedIn_IsMountedWithItsTrigger()
	{
		AddAuthorization().SetAuthorized("alice");

		var cut = RenderLayout();

		await Assert.That(cut.FindComponents<Stub<GlobalSearch>>()).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll(".search-trigger")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Search_WhenTheLauncherAsksForTheBlocklist_OpensForItUntilClosed()
	{
		AddAuthorization().SetAuthorized("alice");
		var cut = RenderLayout();

		await cut.InvokeAsync(() => Services.GetRequiredService<SearchLauncher>().Open(SearchPurpose.Blocklist));

		var search = cut.FindComponent<Stub<GlobalSearch>>().Instance.Parameters;
		await Assert.That(search.Get(global => global.IsOpen)).IsTrue();
		await Assert.That(search.Get(global => global.Purpose)).IsEqualTo(SearchPurpose.Blocklist);

		await cut.InvokeAsync(() => search.Get(global => global.IsOpenChanged).InvokeAsync(false));

		var closed = cut.FindComponent<Stub<GlobalSearch>>().Instance.Parameters;
		await Assert.That(closed.Get(global => global.IsOpen)).IsFalse();
		await Assert.That(closed.Get(global => global.Purpose)).IsEqualTo(SearchPurpose.Wantlist);
	}

	[Test]
	public async Task HomeLink_OnTheHomePage_IsMarkedActive()
	{
		AddAuthorization().SetAuthorized("alice");

		var cut = RenderLayout();

		await Assert.That(cut.Find("a[aria-label='Sammlarr home']").ClassList).Contains("active");
	}

	[Test]
	public async Task HomeLink_OnAnotherPage_IsNotMarkedActive()
	{
		AddAuthorization().SetAuthorized("alice");
		Services.GetRequiredService<NavigationManager>().NavigateTo("/feeds");

		var cut = RenderLayout();

		await Assert.That(cut.Find("a[aria-label='Sammlarr home']").ClassList).DoesNotContain("active");
	}

	[Test]
	public async Task Footer_ShowsTheVersionTheAppRuns()
	{
		AddAuthorization().SetAuthorized("alice");

		var cut = RenderLayout();

		var running = Services.GetRequiredService<AppVersionProvider>().Current.Number;
		await Assert.That(cut.Find("footer a").TextContent).Contains(running);
	}

	[Test]
	public async Task Footer_WhenANewerReleaseIsOut_MarksTheVersion()
	{
		AddAuthorization().SetAuthorized("alice");
		var versions = new AppVersionProvider(new ReleaseOptions(), new AppVersion("1.4.0"));
		var releases = IReleaseFeed.Mock();
		releases.LatestAsync(Any(), Any()).Returns(Some.Of(new PublishedRelease("v1.5.0", new Uri("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0"))));
		Services.AddSingleton(versions);
		Services.AddSingleton(new AppUpdateChecker(versions, releases.Object, IDateTimeProvider.Mock().Object));

		var cut = RenderLayout();

		await Assert.That(cut.WaitForElement("[data-testid=footer-update-dot]")).IsNotNull();
	}

	private IRenderedComponent<MainLayout> RenderLayout() => Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, _ => { }));
}
