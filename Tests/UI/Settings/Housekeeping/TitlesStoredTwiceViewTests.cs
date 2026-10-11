using Contracts.Media;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using UI.Features.Settings.Components.Housekeeping;
using UI.Shared.Services.Branding;
using static UI.Tests.Settings.Housekeeping.StoredTwicePairs;

namespace UI.Tests.Settings.Housekeeping;

public class TitlesStoredTwiceViewTests
{
	private static readonly MediaProviderBrandingService Branding = new();

	private static readonly UserId Me = TestCurrentUser.DefaultId;

	[Test]
	public async Task Plain_ForRemakesOfOneName_NamesBothYears()
	{
		var pair = Pair(
			Title("Annie", 1982, TmdbId.From("15739", MediaType.Movie)),
			Title("Annie", 2014, TmdbId.From("196867", MediaType.Movie)),
			new Undecided(),
			0
		);

		await Assert.That(TitlesStoredTwiceView.Plain(pair)).IsEqualTo("Annie (1982 and 2014)");
	}

	[Test]
	public async Task Plain_ForATitleWhoseOtherHalfWasTrimmed_NamesTheDetailedOne()
	{
		var pair = Pair(
			Title("Dune: Part Two", 2024, TmdbId.From("693134", MediaType.Movie)),
			Title(null, null, ImdbId.From("tt15239678", MediaType.Movie)),
			new Undecided(),
			0
		);

		await Assert.That(TitlesStoredTwiceView.Plain(pair)).IsEqualTo("Dune: Part Two (2024)");
		await Assert.That(TitlesStoredTwiceView.Name(pair.Other)).IsEqualTo("IMDb tt15239678");
	}

	[Test]
	public async Task Plain_ForAPairWhoseHeldTitleWasTrimmed_NamesTheOtherOne()
	{
		var pair = Pair(
			Title(null, null, ImdbId.From("tt0083564", MediaType.Movie)),
			Title("Annie", 1982, TmdbId.From("15739", MediaType.Movie)),
			new Undecided(),
			0
		);

		await Assert.That((TitlesStoredTwiceView.Plain(pair), TitlesStoredTwiceView.Name(TitlesStoredTwiceView.Shown(pair)))).IsEqualTo(("Annie (1982)", "Annie"));
	}

	[Test]
	public async Task Reason_ForAPairASourceMappedElsewhere_SaysWhoAndWhere()
	{
		var answered = new AnsweredElsewhere(RemoteService.Mdblist, TmdbId.From("15739", MediaType.Movie));
		var pair = Pair(
			Title("Annie", 1982, ImdbId.From("tt0083564", MediaType.Movie)),
			Title("Annie", 2014, TmdbId.From("196867", MediaType.Movie)),
			new Distinct(answered, Now),
			1
		);

		await Assert.That(TitlesStoredTwiceView.Short(pair.Note, Me, Branding)).IsEqualTo("MDBList maps it elsewhere");
		await Assert.That(TitlesStoredTwiceView.Reason(pair, Me, Branding)).EndsWith("MDBList says it is TMDB 15739, not 196867");
	}

	[Test]
	public async Task Contradicts_ForAPairASourceMappedElsewhereWhoseRowsNowBothHoldATmdbId_SaysItCannotBeMerged()
	{
		var answered = new Distinct(new AnsweredElsewhere(RemoteService.Mdblist, TmdbId.From("15739", MediaType.Movie)), Now);
		var clashing = Pair(Title("Annie", 1982, TmdbId.From("15739", MediaType.Movie)), Title("Annie", 2014, TmdbId.From("196867", MediaType.Movie)), answered, 1);
		var apart = Pair(
			Title("Annie", 1982, ImdbId.From("tt0083564", MediaType.Movie)),
			Title("Annie", 2014, TmdbId.From("196867", MediaType.Movie)),
			answered,
			1
		);

		await Assert.That((TitlesStoredTwiceView.Contradicts(clashing), TitlesStoredTwiceView.Contradicts(apart))).IsEqualTo((true, false));
	}

	[Test]
	public async Task Waiting_ForOnePairOfEach_SpeaksOfOnePair()
	{
		var text = TitlesStoredTwiceView.Waiting(1, 1);

		await Assert.That(text).StartsWith("1 pair that may be one title.").And.EndsWith("1 pair is kept apart.");
	}

	[Test]
	public async Task Uses_ForATitleOnTwoWantlistsAndInOneFeed_SaysBoth()
	{
		await Assert.That(TitlesStoredTwiceView.Uses(new TitleUses(2, 1))).IsEqualTo("on 2 wantlists · in 1 feed");
	}
}
