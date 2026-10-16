using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class MdblistPageLinkerTests
{
	private static readonly MdblistPageLinker Testee = new();

	[Test]
	[Arguments(MediaType.Movie, "https://mdblist.com/movie/tt1375666")]
	[Arguments(MediaType.Show, "https://mdblist.com/show/tt1375666")]
	public async Task PageOf_ForAnMdblistId_IsItsPageForTheType(MediaType type, string page)
	{
		await Assert.That(Testee.PageOf([MdblistId.From("tt1375666", type)], new None())).IsSome().And.IsEqualTo(page);
	}

	[Test]
	public async Task PageOf_ForAnMdblistIdOfNoKnownType_IsNone()
	{
		await Assert.That(Testee.PageOf([MdblistId.From("tt1375666", MediaType.Unknown)], new None())).IsNone();
	}

	[Test]
	public async Task PageOf_WithoutAnMdblistId_IsNone()
	{
		await Assert.That(Testee.PageOf([TmdbId.From("27205", MediaType.Movie)], new None())).IsNone();
	}
}
