using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class ImdbPageLinkerTests
{
	private static readonly ImdbPageLinker Testee = new();

	[Test]
	public async Task PageOf_ForAnImdbId_IsItsTitlePage()
	{
		await Assert.That(Testee.PageOf([ImdbId.From("tt1375666", MediaType.Movie)], new None())).IsSome().And.IsEqualTo("https://www.imdb.com/title/tt1375666");
	}

	[Test]
	public async Task PageOf_WithoutAnImdbId_IsNone()
	{
		await Assert.That(Testee.PageOf([TmdbId.From("27205", MediaType.Movie)], Some.Of("Inception"))).IsNone();
	}
}
