using Bunit;
using Domain.Identity;
using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding;

public class PersonAvatarTests : BunitContext
{
	[Test]
	public async Task Avatar_ForTheSamePerson_AlwaysDrawsTheSameBird()
	{
		var person = UserId.New();

		var first = Render<PersonAvatar>(parameters => parameters.Add(avatar => avatar.Person, person).Add(avatar => avatar.Hue, 40));
		var again = Render<PersonAvatar>(parameters => parameters.Add(avatar => avatar.Person, person).Add(avatar => avatar.Hue, 40));

		await Assert.That(first.Markup).IsEqualTo(again.Markup);
	}

	[Test]
	public async Task Avatar_TakesItsColourFromTheStoredHue()
	{
		var cut = Render<PersonAvatar>(parameters => parameters.Add(avatar => avatar.Person, UserId.New()).Add(avatar => avatar.Hue, 177.5));

		await Assert.That(cut.Find("[data-bird]").GetAttribute("style")).StartsWith("--bird-hue: 177.5;");
		await Assert.That(cut.Find("svg").InnerHtml).Contains("fill=\"var(--bird-body)\"");
	}
}
