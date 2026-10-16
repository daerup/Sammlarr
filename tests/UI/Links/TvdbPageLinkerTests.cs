using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class TvdbPageLinkerTests
{
	private static readonly TvdbPageLinker Testee = new();

	[Test]
	[Arguments(MediaType.Movie, "https://thetvdb.com/dereferrer/movie/81189")]
	[Arguments(MediaType.Show, "https://thetvdb.com/dereferrer/series/81189")]
	public async Task PageOf_ForATvdbId_IsItsPageForTheType(MediaType type, string page)
	{
		await Assert.That(Testee.PageOf([TvdbId.From("81189", type)], new None())).IsSome().And.IsEqualTo(page);
	}

	[Test]
	public async Task PageOf_ForATvdbIdOfNoKnownType_IsNone()
	{
		await Assert.That(Testee.PageOf([TvdbId.From("81189", MediaType.Unknown)], new None())).IsNone();
	}

	[Test]
	public async Task PageOf_WithoutATvdbId_IsNone()
	{
		await Assert.That(Testee.PageOf([TmdbId.From("1396", MediaType.Show)], Some.Of("Breaking Bad"))).IsNone();
	}
}
