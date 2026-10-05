using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class RottenTomatoesPageLinkerTests
{
	private static readonly RottenTomatoesPageLinker Testee = new();

	[Test]
	public async Task PageOf_ForARottenTomatoesId_IsItsPage()
	{
		var page = Testee.PageOf([RottenTomatoesId.From("m/inception", MediaType.Movie)], Some.Of("Inception"));

		await Assert.That(page).IsSome().And.IsEqualTo("https://www.rottentomatoes.com/m/inception");
	}

	[Test]
	public async Task PageOf_WithoutAnIdButWithATitle_SearchesForTheTitle()
	{
		var page = Testee.PageOf([ImdbId.From("tt1375666", MediaType.Movie)], Some.Of("Last Seen"));

		await Assert.That(page).IsSome().And.IsEqualTo("https://www.rottentomatoes.com/search?search=Last%20Seen");
	}

	[Test]
	public async Task PageOf_WithoutAnIdOrATitle_IsNone()
	{
		await Assert.That(Testee.PageOf([ImdbId.From("tt1375666", MediaType.Movie)], new None())).IsNone();
	}
}
