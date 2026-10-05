using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class NoPageLinkerTests
{
	private static readonly NoPageLinker Testee = new();

	[Test]
	public async Task PageOf_EvenForATitleWithIdsAndATitle_IsNone()
	{
		await Assert.That(Testee.PageOf([RadarrId.From("12", MediaType.Movie), TmdbId.From("27205", MediaType.Movie)], Some.Of("Inception"))).IsNone();
	}
}
