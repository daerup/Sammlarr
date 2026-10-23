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
	private static readonly Guid Invited = Guid.Parse("6a3c1f4e-2b7d-4e8a-9f10-3c5d7e9a1b2c");

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
	public async Task Card_ForAnInvite_NamesTheirAccountAndPostsTheJoin()
	{
		_links
			.InspectAsync(AccountLinkKind.Invite, UserId.From(Invited), new Secret("token"), Any())
			.Returns(Some.Of(new AccountLinkPreview(AccountLinkKind.Invite, "mira")));

		var cut = Render<PasswordLinkCard>(parameters =>
			parameters.Add(card => card.Kind, AccountLinkKind.Invite).Add(card => card.User, Invited).Add(card => card.Token, "token")
		);

		var paragraph = cut.WaitForElement("p");
		await Assert.That(paragraph.TextContent.Trim()).StartsWith("You were invited as");
		await Assert.That(paragraph.QuerySelector("span")?.TextContent).IsEqualTo("mira");
		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/auth/join");
		await Assert.That(cut.Find("input[name=user]").GetAttribute("value")).IsEqualTo(Invited.ToString());
		await Assert.That(cut.Find("input[name=token]").GetAttribute("value")).IsEqualTo("token");
	}

	[Test]
	public async Task Pages_ForEachKindOfLink_RenderTheirCard()
	{
		_links.InspectAsync(Any<AccountLinkKind>(), UserId.From(Invited), Any(), Any()).Returns(new None());

		var join = Render<Join>(parameters => parameters.Add(page => page.User, Invited).Add(page => page.Token, "token"));
		var reset = Render<ResetPassword>(parameters => parameters.Add(page => page.User, Invited).Add(page => page.Token, "token"));

		await Assert.That(join.FindComponents<PasswordLinkCard>()).Count().IsEqualTo(1);
		await Assert.That(reset.FindComponents<PasswordLinkCard>()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Card_ForALinkThatNoLongerWorks_SaysToAskForANewOne()
	{
		_links.InspectAsync(Any<AccountLinkKind>(), UserId.From(Invited), Any(), Any()).Returns(new None());

		var cut = Render<PasswordLinkCard>(parameters =>
			parameters.Add(card => card.Kind, AccountLinkKind.Reset).Add(card => card.User, Invited).Add(card => card.Token, "used")
		);

		await Assert.That(cut.WaitForElement("h2").TextContent.Trim()).IsEqualTo("This link no longer works");
		await Assert.That(cut.FindAll("form")).IsEmpty();
	}
}
