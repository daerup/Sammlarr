using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Identity;

namespace UI.Tests.Identity;

public class UserAvatarTests : BunitContext
{
	[Before(Test)]
	public void Setup() => Services.AddBlazorBlueprintComponents();

	[Test]
	[Arguments(" daerup", "D")]
	[Arguments("", "")]
	public async Task Avatar_WithoutAPicture_ShowsTheNamesFirstLetter(string name, string initial)
	{
		var cut = Render<UserAvatar>(parameters => parameters.Add(avatar => avatar.Name, name));

		await Assert.That(cut.Find("span").TextContent.Trim()).IsEqualTo(initial);
		await Assert.That(cut.FindAll("img")).IsEmpty();
	}
}
