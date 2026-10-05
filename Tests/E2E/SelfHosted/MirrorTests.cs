using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class MirrorTests
{
	[Test]
	public async Task Movie_RemovedFromTheWantlistAndSynced_IsDeletedFromRadarr()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);
		await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);

		await SelfHostedWantlist.RemoveAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		await stack.Radarr.WaitForMovieGoneAsync(Titles.ProjectHailMaryTmdb);
	}

	[Test]
	public async Task Movie_InRadarr_IsLeftAloneUntilTaggedForThePersonThenFollowsTheirWantlistUntilDeletedThere()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		var movie = await stack.Radarr.AddMovieAsync(Titles.ProjectHailMaryTmdb);
		var preview = await SelfHostedWantlist.OpenSyncAsync(page);
		await Expect(preview.GetByText("0 titles compared")).ToBeVisibleAsync(Wait.Settle);
		await Expect(preview.Text("Everything is in sync.")).ToBeVisibleAsync();

		await stack.Radarr.TagMovieAsync(movie, Titles.AdminTag);
		await SelfHostedWantlist.SyncAsync(page);

		await Expect(SelfHostedWantlist.Tile(page, Titles.ProjectHailMary)).ToBeVisibleAsync(Wait.Settle);
		await stack.Radarr.DeleteMovieAsync(movie);
		await SelfHostedWantlist.SyncAsync(page);
		await Expect(page.GetByText("Your wantlist is empty.")).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task TwoPeople_EachSyncingTheirOwnTitle_StayApart()
	{
		var stack = await SelfHostedApp.StackAsync();
		var admin = await SelfHostedApp.OpenPageAsync();
		var invite = await SelfHostedSetup.FinishAsync(admin, stack);
		var member = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.JoinWithTmdbKeyAsync(member, invite);
		await SelfHostedWantlist.AddAsync(admin, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(admin);
		await SelfHostedWantlist.SearchWithRadarrAndSonarrAsync(admin);

		await SelfHostedWantlist.AddAsync(member, Titles.BreakingBad);
		await SelfHostedWantlist.SyncAsync(member);

		var series = await stack.Sonarr.WaitForSeriesAsync(Titles.BreakingBadTvdb);
		var movie = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		await Assert.That(ArrApi.TagsOf(series)).IsEquivalentTo([await stack.Sonarr.TagIdAsync(Titles.MemberTag)]);
		await Assert.That(ArrApi.TagsOf(movie)).IsEquivalentTo([await stack.Radarr.TagIdAsync(Titles.AdminTag)]);
		var groups = (await SelfHostedWantlist.OpenSyncAsync(admin)).Locator(".change-group");
		await Expect(groups).ToHaveCountAsync(1, Wait.Settle);
		await Expect(groups).ToContainTextAsync("Already in sync");
		await admin.Keyboard.PressAsync("Escape");
		await Expect(SelfHostedWantlist.Tile(admin, Titles.ProjectHailMary)).ToBeVisibleAsync();
		await Expect(SelfHostedWantlist.Tile(admin, Titles.BreakingBad)).ToHaveCountAsync(0);
		await member.VisitAsync(Routes.Wantlist);
		await Expect(SelfHostedWantlist.Tile(member, Titles.BreakingBad)).ToBeVisibleAsync();
		await Expect(SelfHostedWantlist.Tile(member, Titles.ProjectHailMary)).ToHaveCountAsync(0);
	}
}
