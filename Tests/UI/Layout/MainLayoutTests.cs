using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Contracts.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Identity;
using UI.Shared.Layout;
using UI.Shared.Media;
using UI.Shared.Notifications.WebUi;

namespace UI.Tests.Layout;

public class MainLayoutTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped(_ => new CurrentUserAccessor(new CurrentUserOverride()));
		ComponentFactories.AddStub<GlobalSearch>();
		ComponentFactories.AddStub<ProfileCard>();
		ComponentFactories.AddStub<SessionInvalidationListener>();
		ComponentFactories.AddStub<WebUiToastListener>();
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

	private IRenderedComponent<MainLayout> RenderLayout() => Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(_ => { })));
}
