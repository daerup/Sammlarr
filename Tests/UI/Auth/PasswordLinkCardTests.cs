using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using UI.Features.Auth.Components;
using UI.Features.Auth.Pages;

namespace UI.Tests.Auth;

public class PasswordLinkCardTests : BunitContext
{
	private readonly Mock<IAccountLinkService> _links = IAccountLinkService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(new AccountsOptions());
		Services.AddSingleton(_links.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Card_ForAnInvite_SaysWhoInvitedThemAndPostsTheJoin()
	{
		_links.InspectAsync(AccountLinkKind.Invite, "token", Any()).Returns(Some.Of(new AccountLinkPreview(AccountLinkKind.Invite, "mira", "guest")));

		var cut = Render<PasswordLinkCard>(parameters => parameters.Add(card => card.Kind, AccountLinkKind.Invite).Add(card => card.Token, "token"));

		var paragraph = cut.WaitForElement("p");
		await Assert.That(paragraph.TextContent.Trim()).StartsWith("guest");
		await Assert.That(paragraph.QuerySelector("span")?.TextContent).IsEqualTo("mira");
		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/auth/join");
		await Assert.That(cut.Find("input[name=token]").GetAttribute("value")).IsEqualTo("token");
	}

	[Test]
	public async Task Pages_ForEachKindOfLink_RenderTheirCard()
	{
		_links.InspectAsync(Any<AccountLinkKind>(), Any(), Any()).Returns(new None());

		var join = Render<Join>(parameters => parameters.Add(page => page.Token, "token"));
		var reset = Render<ResetPassword>(parameters => parameters.Add(page => page.Token, "token"));

		await Assert.That(join.FindComponents<PasswordLinkCard>()).Count().IsEqualTo(1);
		await Assert.That(reset.FindComponents<PasswordLinkCard>()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Card_ForALinkThatNoLongerWorks_SaysToAskForANewOne()
	{
		_links.InspectAsync(Any<AccountLinkKind>(), Any(), Any()).Returns(new None());

		var cut = Render<PasswordLinkCard>(parameters => parameters.Add(card => card.Kind, AccountLinkKind.Reset).Add(card => card.Token, "used"));

		await Assert.That(cut.WaitForElement("h2").TextContent.Trim()).IsEqualTo("This link no longer works");
		await Assert.That(cut.FindAll("form")).IsEmpty();
	}
}
