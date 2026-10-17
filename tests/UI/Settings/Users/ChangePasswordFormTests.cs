using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using UI.Features.Settings.Components.Users;

namespace UI.Tests.Settings.Users;

public class ChangePasswordFormTests : BunitContext
{
	private readonly Mock<ILocalAccountService> _accounts = ILocalAccountService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(new AccountsOptions());
		Services.AddSingleton(_accounts.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task ChangePassword_UntilTheNewPasswordIsConfirmed_StaysDisabled()
	{
		var cut = Render<ChangePasswordForm>();

		await TypeAsync(cut, "account-current", "correct horse battery");
		await TypeAsync(cut, "account-next", "staple and a hinge");
		await TypeAsync(cut, "account-confirm", "staple and a hing");

		await Assert.That(ChangeButton(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task ChangePassword_WhenTheCurrentPasswordIsWrong_SaysSo()
	{
		_accounts.ChangePasswordAsync(Any(), Any(), Any()).Returns(Error.From("The current password is not right."));
		var cut = Render<ChangePasswordForm>();
		await TypeAsync(cut, "account-current", "not it at all");
		await TypeAsync(cut, "account-next", "staple and a hinge");
		await TypeAsync(cut, "account-confirm", "staple and a hinge");

		await ChangeButton(cut).ClickAsync();

		await Assert.That(cut.Find("[role=alert]").TextContent.Trim()).IsEqualTo("The current password is not right.");
	}

	[Test]
	public async Task ChangePassword_WhenItWorks_SignsOutToUseTheNewOne()
	{
		_accounts
			.ChangePasswordAsync(Any(), Any(), Any())
			.Returns(Success.Of(new User(UserId.New(), "guest", string.Empty, UserRole.Member, DateTimeOffset.UnixEpoch)));
		var cut = Render<ChangePasswordForm>();
		await TypeAsync(cut, "account-current", "correct horse battery");
		await TypeAsync(cut, "account-next", "staple and a hinge");
		await TypeAsync(cut, "account-confirm", "staple and a hinge");

		await ChangeButton(cut).ClickAsync();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/signin");
	}

	[Test]
	public async Task NewPassword_WithAConfiguredMinimum_AsksForThatManyCharacters()
	{
		Services.AddSingleton(new AccountsOptions { MinimumPasswordLength = 5 });

		var cut = Render<ChangePasswordForm>();

		await Assert.That(cut.Find("#account-next").GetAttribute("placeholder")).IsEqualTo("At least 5 characters");
	}

	private static Task TypeAsync(IRenderedComponent<ChangePasswordForm> cut, string id, string text)
	{
		return cut.InvokeAsync(() => cut.FindComponents<BbInput>().Single(input => input.Instance.Id == id).Instance.ValueChanged.InvokeAsync(text));
	}

	private static IElement ChangeButton(IRenderedComponent<ChangePasswordForm> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Trim() == "Change password");
	}
}
