using Domain.Media;
using TestInfrastructure;
using UI.Shared.Services.Links;

namespace UI.Tests.Links;

public class RatingSitePageLinkerTests
{
	[Test]
	[Arguments(MediaType.Movie, "https://www.metacritic.com/movie/fight-club")]
	[Arguments(MediaType.Show, "https://www.metacritic.com/tv/fight-club")]
	public async Task Metacritic_ForASlug_IsItsPageUnderItsType(MediaType type, string page)
	{
		await Assert.That(new MetacriticPageLinker().PageOf([MetacriticId.From("fight-club", type)], new None())).IsSome().And.IsEqualTo(page);
	}

	[Test]
	public async Task Letterboxd_ForASlug_IsItsFilmPage()
	{
		await Assert
			.That(new LetterboxdPageLinker().PageOf([LetterboxdId.From("fight-club", MediaType.Movie)], new None()))
			.IsSome()
			.And.IsEqualTo("https://letterboxd.com/film/fight-club/");
	}

	[Test]
	[Arguments(MediaType.Movie, "https://app.trakt.tv/movies/432")]
	[Arguments(MediaType.Show, "https://app.trakt.tv/shows/432")]
	public async Task Trakt_ForAnId_OpensItsPageUnderItsType(MediaType type, string page)
	{
		await Assert.That(new TraktPageLinker().PageOf([TraktId.From("432", type)], new None())).IsSome().And.IsEqualTo(page);
	}

	[Test]
	public async Task Trakt_ForAnIdOfNoKnownType_IsNone()
	{
		await Assert.That(new TraktPageLinker().PageOf([TraktId.From("432", MediaType.Unknown)], new None())).IsNone();
	}

	[Test]
	public async Task Mal_ForAnId_IsItsAnimePage()
	{
		await Assert
			.That(new MalPageLinker().PageOf([MalId.From("5114", MediaType.Show)], new None()))
			.IsSome()
			.And.IsEqualTo("https://myanimelist.net/anime/5114");
	}

	[Test]
	public async Task EachRatingSite_WithoutItsId_IsNone()
	{
		IReadOnlyCollection<IExternalMediaId> ids = [ImdbId.From("tt0137523", MediaType.Movie)];

		await Assert.That(new MetacriticPageLinker().PageOf(ids, Some.Of("Fight Club"))).IsNone();
		await Assert.That(new LetterboxdPageLinker().PageOf(ids, Some.Of("Fight Club"))).IsNone();
		await Assert.That(new TraktPageLinker().PageOf(ids, Some.Of("Fight Club"))).IsNone();
		await Assert.That(new MalPageLinker().PageOf(ids, Some.Of("Fight Club"))).IsNone();
	}
}
