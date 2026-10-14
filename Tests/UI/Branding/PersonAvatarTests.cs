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

		var first = Render<PersonAvatar>(parameters => parameters.Add(avatar => avatar.Person, person).Add(avatar => avatar.Colour, PersonColour.Golden(40)));
		var again = Render<PersonAvatar>(parameters => parameters.Add(avatar => avatar.Person, person).Add(avatar => avatar.Colour, PersonColour.Golden(40)));

		await Assert.That(first.Markup).IsEqualTo(again.Markup);
	}
}
