using UI.Shared.Branding.Birds;

namespace UI.Tests.Branding.Birds;

public class PersonHuesTests
{
	[Test]
	public async Task After_EachNextPerson_StepsTheGoldenAngleRoundTheWheel()
	{
		double[] hues = [PersonHues.After(new None())];
		for (var person = 1; person < 5; person++)
		{
			hues = [.. hues, PersonHues.After(Some.Of(hues[^1]))];
		}

		await Assert.That(hues).IsEquivalentTo([40, 177.5, 315, 92.5, 230]);
	}
}
