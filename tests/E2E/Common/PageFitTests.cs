namespace E2E.Tests.Common;

[Devices(Device.Desktop, Device.Phone)]
public class PageFitTests(Device device) : E2ETests(device)
{
	[Test]
	[Arguments(Routes.SignIn, Viewer.Guest)]
	[Arguments(Routes.Home, Viewer.Admin)]
	[Arguments(Routes.Browse, Viewer.Admin)]
	[Arguments(Routes.Feeds, Viewer.Admin)]
	[Arguments(Routes.Wantlist, Viewer.Admin)]
	[Arguments(Routes.Blocklist, Viewer.Admin)]
	[Arguments(Routes.CreateFeed, Viewer.Admin)]
	public async Task Page_RunsNothingPastTheRightEdge(string route, Viewer viewer)
	{
		var page = viewer switch
		{
			Viewer.Guest => await E2EApp.GuestAt(route),
			Viewer.Admin => await E2EApp.AdminAt(route),
			Viewer.Member => await E2EApp.MemberAt(route),
		};

		await Assert.That(await page.OverflowingAsync()).IsEmpty();
	}
}
