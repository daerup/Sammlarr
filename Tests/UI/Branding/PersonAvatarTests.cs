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

	[Test]
	public async Task Avatar_ForAGoldenColour_TakesTheStoredHueInTheAppsTone()
	{
		var cut = Render<PersonAvatar>(parameters =>
			parameters.Add(avatar => avatar.Person, UserId.New()).Add(avatar => avatar.Colour, PersonColour.Golden(177.5))
		);

		await Assert.That(cut.Find("[data-bird]").GetAttribute("style")).StartsWith("--bird-hue: 177.5;").And.DoesNotContain("--bird-chroma");
		await Assert.That(cut.Find("svg").InnerHtml).Contains("fill=\"var(--bird-body)\"");
	}

	[Test]
	public async Task Avatar_ForAPictureColour_CarriesItsChromaAndScalesTheToneByIt()
	{
		var cut = Render<PersonAvatar>(parameters =>
			parameters.Add(avatar => avatar.Person, UserId.New()).Add(avatar => avatar.Colour, new PersonColour(90.7, Some.Of(0.042)))
		);

		await Assert.That(cut.Find("[data-bird]").GetAttribute("style")).StartsWith("--bird-hue: 90.7; --bird-chroma: 0.042; --bird-scale: 0.35;");
	}
}
