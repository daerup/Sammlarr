using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Contracts.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Users;

namespace UI.Tests.Settings;

public class InviteDialogTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(_users.Object);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		Services.AddSingleton(clock.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task CreateLink_ForAFreeUsername_ShowsTheLinkAndHowLongItWorks()
	{
		_users.InviteAsync("mira", Any()).Returns(Success.Of(new IssuedLink("/join/abc", Now.AddDays(7))));
		var cut = await OpenAsync("mira");

		await Assert.That(cut.WaitForElement("[data-link]").TextContent).EndsWith("/join/abc");
		await Assert.That(cut.FindComponent<BbDialogDescription>().Find("*").TextContent).Contains("7 days").And.Contains("mira");
	}

	[Test]
	public async Task CreateLink_ForATakenUsername_SaysWhy()
	{
		_users.InviteAsync(Any(), Any()).Returns(Error.From("mira already has an account or an invite."));
		var cut = await OpenAsync("mira");

		await Assert.That(cut.WaitForElement("[role=alert]").TextContent.Trim()).IsEqualTo("mira already has an account or an invite.");
		await Assert.That(cut.FindAll("[data-link]")).IsEmpty();
	}

	private async Task<IRenderedComponent<IComponent>> OpenAsync(string username)
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<InviteDialog>(1);
			builder.CloseComponent();
		});
		await cut.FindAll("button").Single(button => button.TextContent.Contains("Invite member")).ClickAsync();
		await cut.InvokeAsync(() =>
			cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "invite-username").Instance.ValueChanged.InvokeAsync(username)
		);
		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Create link").ClickAsync();
		return cut;
	}
}
