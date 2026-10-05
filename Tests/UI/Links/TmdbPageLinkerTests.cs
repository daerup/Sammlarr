using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class TmdbPageLinkerTests
{
	private static readonly TmdbPageLinker Testee = new();

	[Test]
	[Arguments(MediaType.Movie, "https://www.themoviedb.org/movie/27205")]
	[Arguments(MediaType.Show, "https://www.themoviedb.org/tv/27205")]
	[Arguments(MediaType.Unknown, "https://www.themoviedb.org/person/27205")]
	public async Task PageOf_ForATmdbId_IsItsPageForTheType(MediaType type, string page)
	{
		await Assert.That(Testee.PageOf([TmdbId.From("27205", type)], new None())).IsSome().And.IsEqualTo(page);
	}

	[Test]
	public async Task PageOf_WithoutATmdbId_IsNone()
	{
		await Assert.That(Testee.PageOf([ImdbId.From("tt1375666", MediaType.Movie)], Some.Of("Inception"))).IsNone();
	}
}
