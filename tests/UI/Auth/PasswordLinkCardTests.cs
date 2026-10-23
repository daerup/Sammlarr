using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Identity;
using Microsoft.AspNetCore.Components;
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
	public async Task Pages_ForEachKindOfLink_HandTheirCardTheTokenFromTheQuery()
	{
		_links.InspectAsync(Any<AccountLinkKind>(), UserId.From(Invited), Any(), Any()).Returns(new None());
		var navigation = Services.GetRequiredService<NavigationManager>();

		navigation.NavigateTo($"/join/{Invited}?token=join-token");
		var join = Render<Join>(parameters => parameters.Add(page => page.User, Invited));
		await Assert.That(join.FindComponent<PasswordLinkCard>().Instance.Token).IsEqualTo("join-token");

		navigation.NavigateTo($"/reset/{Invited}?token=reset-token");
		var reset = Render<ResetPassword>(parameters => parameters.Add(page => page.User, Invited));
		await Assert.That(reset.FindComponent<PasswordLinkCard>().Instance.Token).IsEqualTo("reset-token");
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
