using BlazorBlueprint.Components;
using Bunit;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Feed;
using Domain.Identity;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Common;

namespace UI.Tests.Settings.Providers;

public class ArrSinkRowTests : BunitContext
{
	private static readonly ArrSinkConfig Connected = new(
		ArrInstanceConfig.Empty with
		{
			Url = Some.Of("http://radarr:7878"),
			ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
			QualityProfileId = Some.Of(4),
			RootFolderPath = Some.Of("/movies"),
		},
		ArrInstanceConfig.Empty with
		{
			Url = Some.Of("http://sonarr:8989"),
			ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
			QualityProfileId = Some.Of(4),
			RootFolderPath = Some.Of("/tv"),
		},
		ArrTagScheme.Default
	);

	private static readonly ArrInstanceConfig AnimeSonarr = Connected.Sonarr with { AnimeRootFolderPath = Some.Of("/anime"), AnimeQualityProfileId = Some.Of(6) };

	private readonly Mock<IArrSinkConfigService> _configs = IArrSinkConfigService.Mock();

	private readonly Mock<IArrConnectionTester> _tester = IArrConnectionTester.Mock();

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	private static readonly User Guest = new(TestCurrentUser.DefaultId, "Guest", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch);

	private int _switchRequests;

	private int _stayRequests;

	private readonly List<ArrSinkConfig> _moves = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(_configs.Object);
		Services.AddSingleton(_tester.Object);
		_users.GetAllAsync(Any()).Returns([Guest]);
		Services.AddSingleton(_users.Object);
		var feeds = IFeedService.Mock();
		feeds.GetAsync(Any()).Returns(new FeedSnapshot([]));
		Services.AddSingleton(feeds.Object);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		JSInterop.Mode = JSRuntimeMode.Loose;
		_configs.GetAsync(Any()).Returns(Connected);
		Answers(ArrApp.Radarr, "/movies");
		Answers(ArrApp.Sonarr, "/tv");
	}

	[Test]
	public async Task Row_WhenBothAppsAnswer_SaysConnectedNextToItsRolesAndOpensOnRadarrBeforeSonarrAndTags()
	{
		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		await Assert.That(cut.FindAll(".arr-row-head .provider-role .sr-only").Select(role => role.TextContent)).IsEquivalentTo(["Wantlist", "Search"]);
		await Assert.That(cut.Markup).DoesNotContain("Connection · wantlist");
		await Assert.That(Tabs(cut).Select(Name)).IsEquivalentTo(["Radarr", "Sonarr", "Tags"], CollectionOrdering.Matching);
		await Assert.That(cut.FindAll("[role=tab]").Select(tab => tab.TextContent)).DoesNotContain(text => text.Contains("movies") || text.Contains("shows"));
		await Assert.That(Name(cut.Find("[role=tab][aria-selected=true]"))).IsEqualTo("Radarr");
	}

	[Test]
	public async Task Switch_InSetupBeforeEitherAppIsConnected_IsDisabled()
	{
		_configs.GetAsync(Any()).Returns(ArrSinkConfig.Empty);

		var cut = RenderRow(settingUp: true);

		cut.WaitForState(() => cut.FindAll("button").Any(button => button.TextContent.Contains("Switch the wantlist")));
		await Assert.That(SwitchButton(cut).HasAttribute("disabled")).IsTrue();
		await Assert.That(Badge(cut)).IsEqualTo("Needs setup");
	}

	[Test]
	public async Task Switch_InSetupOnceBothAppsAnswer_AsksToSwitch()
	{
		var cut = RenderRow(settingUp: true);

		cut.WaitForState(() => !SwitchButton(cut).HasAttribute("disabled"));
		await SwitchButton(cut).ClickAsync();

		await Assert.That(_switchRequests).IsEqualTo(1);
	}

	[Test]
	public async Task StayOnPlex_InSetup_AsksToStayOnPlex()
	{
		var cut = RenderRow(settingUp: true);

		cut.WaitForState(() => cut.Markup.Contains("Stay on Plex"));
		await StayOnPlexButton(cut).ClickAsync();

		await Assert.That(_stayRequests).IsEqualTo(1);
	}

	[Test]
	public async Task Test_WhenNothingIsChosenYet_StoresTheAppsFirstProfileAndFolder()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Radarr = Connected.Radarr with { QualityProfileId = new None(), RootFolderPath = new None() } });

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		_configs.StoreAsync(Is<ArrSinkConfig>(config => config!.Radarr == Connected.Radarr), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Test_WhenTheAppNoLongerHasTheAnimeFolder_TurnsTheAnimeSlotOff()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Sonarr = AnimeSonarr });

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		_configs.StoreAsync(Is<ArrSinkConfig>(config => config!.Sonarr == Connected.Sonarr), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Test_WhileTheAppStillHasTheAnimeFolderAndProfile_KeepsThem()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Sonarr = AnimeSonarr });
		SonarrAnswers(new ArrQualityProfile(4, "HD-1080p"), new ArrQualityProfile(6, "Anime"));

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		_configs.StoreAsync(Any<ArrSinkConfig>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Test_WhenTheAppNoLongerHasTheAnimeProfile_KeepsTheAnimeFolderWithTheDefaultProfile()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Sonarr = AnimeSonarr });
		SonarrAnswers(new ArrQualityProfile(4, "HD-1080p"));

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		_configs.StoreAsync(Is<ArrSinkConfig>(config => config!.Sonarr == AnimeSonarr with { AnimeQualityProfileId = new None() }), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Test_WithTwoFolders_PicksOnlyTheDefaultOne()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Radarr = Connected.Radarr with { RootFolderPath = new None() } });
		_tester
			.TestAsync(ArrApp.Radarr, Any<ArrInstanceConfig>(), Any())
			.Returns(
				Success.Of(new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p")], [new ArrRootFolder("/movies", 1), new ArrRootFolder("/anime-movies", 1)]))
			);

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		_configs.StoreAsync(Is<ArrSinkConfig>(config => config!.Radarr == Connected.Radarr), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Switch_WhenAnAppHasNoRootFolderYet_StaysDisabled()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Sonarr = Connected.Sonarr with { RootFolderPath = new None() } });
		_tester.TestAsync(ArrApp.Sonarr, Any<ArrInstanceConfig>(), Any()).Returns(Success.Of(new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p")], [])));

		var cut = RenderRow(settingUp: true);

		cut.WaitForState(() => cut.Markup.Contains("Sonarr has no root folder yet"));
		await Assert.That(SwitchButton(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Test_WhenAnActiveAppGetsANewAddress_AsksForEveryTitleToMoveAcross()
	{
		var cut = RenderRow(settingUp: false);
		cut.WaitForState(() => Badge(cut) == "Connected");

		var radarr = cut.FindComponents<ArrAppSection>().First(section => section.Instance.App == ArrApp.Radarr);
		await cut.InvokeAsync(() => radarr.Instance.Test.InvokeAsync(Connected.Radarr with { Url = Some.Of("http://radarr.lan:7878") }));

		await Assert.That(_moves.Single().Radarr.Url).IsSome().And.IsEqualTo("http://radarr.lan:7878");
		_configs.StoreAsync(Any<ArrSinkConfig>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Test_WhenAnActiveAppAnswersAtTheSameAddress_MovesNothing()
	{
		var cut = RenderRow(settingUp: false);
		cut.WaitForState(() => Badge(cut) == "Connected");

		var radarr = cut.FindComponents<ArrAppSection>().First(section => section.Instance.App == ArrApp.Radarr);
		await cut.InvokeAsync(() => radarr.Instance.Test.InvokeAsync(Connected.Radarr));

		await Assert.That(_moves).IsEmpty();
	}

	[Test]
	public async Task Test_WhenOnlySearchUsesAnAppThatGetsANewAddress_StoresItWithoutMovingTitles()
	{
		var cut = RenderRow(settingUp: false, syncs: false);
		cut.WaitForState(() => Badge(cut) == "Connected");

		var radarr = cut.FindComponents<ArrAppSection>().First(section => section.Instance.App == ArrApp.Radarr);
		await cut.InvokeAsync(() => radarr.Instance.Test.InvokeAsync(Connected.Radarr with { Url = Some.Of("http://radarr.lan:7878") }));

		await Assert.That(_moves).IsEmpty();
		_configs.StoreAsync(Any<ArrSinkConfig>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Tags_ForAPersonRenamedSinceTheyJoined_PreviewTheNameTheyJoinedWith()
	{
		_users.GetAllAsync(Any()).Returns([Guest with { DisplayName = "Renamed" }]);

		var cut = RenderRow(settingUp: false);
		cut.WaitForState(() => Badge(cut) == "Connected");

		await Tab(cut, "Tags").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindComponent<ArrTagsSection>().Instance.Users).IsEquivalentTo([new ArrTagUser(TestCurrentUser.DefaultId.Value, "Guest")]);
	}

	[Test]
	[Arguments(true, false)]
	[Arguments(false, false)]
	public async Task Tabs_WhileSettingUpOrForSearchOnly_LeaveTagsOut(bool settingUp, bool syncs)
	{
		var cut = RenderRow(settingUp, syncs);

		cut.WaitForState(() => Tabs(cut).Count == 2);
		await Assert.That(Tabs(cut).Select(Name)).IsEquivalentTo(["Radarr", "Sonarr"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Tabs_WhenSonarrFailsItsTest_OpenOnSonarr()
	{
		_tester.TestAsync(ArrApp.Sonarr, Any<ArrInstanceConfig>(), Any()).Returns(Error.From("Sonarr answered 401 Unauthorized at http://sonarr:8989."));

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Sonarr not answering");
		await Assert.That(Name(cut.Find("[role=tab][aria-selected=true]"))).IsEqualTo("Sonarr");
		await Assert.That(Tab(cut, "Sonarr").TextContent).Contains("Test failed");
	}

	[Test]
	public async Task Tabs_WhenTagsAreOpenedWhileTheAppsAreStillTested_StayOnTags()
	{
		var sonarr = new TaskCompletionSource<Result<ArrInstanceChoices>>();
		_tester.TestAsync(ArrApp.Sonarr, Any<ArrInstanceConfig>(), Any()).ReturnsAsync(sonarr.Task);
		var cut = RenderRow(settingUp: false);
		cut.WaitForState(() => Tabs(cut).Count == 3);

		await Tab(cut, "Tags").ClickAsync(new MouseEventArgs());
		sonarr.SetResult(Success.Of(new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p")], [new ArrRootFolder("/tv", 1)])));

		cut.WaitForState(() => Badge(cut) == "Connected");
		await Assert.That(Name(cut.Find("[role=tab][aria-selected=true]"))).IsEqualTo("Tags");
	}

	[Test]
	public async Task Tabs_WhenSwitchedAndBack_KeepAKeyTypedButNotTested()
	{
		var cut = RenderRow(settingUp: false);
		cut.WaitForState(() => Badge(cut) == "Connected");
		var radarr = cut.FindComponents<ArrAppSection>().First(section => section.Instance.App == ArrApp.Radarr);
		await cut.InvokeAsync(() => radarr.FindComponent<SensitiveInput>().Instance.ValueChanged.InvokeAsync("a-key-not-tested-yet"));

		await Tab(cut, "Sonarr").ClickAsync(new MouseEventArgs());
		await Tab(cut, "Radarr").ClickAsync(new MouseEventArgs());

		await Assert.That(radarr.FindComponent<SensitiveInput>().Instance.Value).IsEqualTo("a-key-not-tested-yet");
	}

	[Test]
	public async Task Setup_WhileSettingUp_SitsUnderTheTabsNotInOne()
	{
		var cut = RenderRow(settingUp: true);

		cut.WaitForState(() => cut.FindAll(".arr-shared").Count == 1);
		await Assert.That(cut.Find(".arr-shared").Closest("[role=tabpanel]")).IsNull();
	}

	private static IReadOnlyList<AngleSharp.Dom.IElement> Tabs(IRenderedComponent<ArrSinkRow> cut) => cut.FindAll("[role=tab]");

	private static AngleSharp.Dom.IElement Tab(IRenderedComponent<ArrSinkRow> cut, string name) => Tabs(cut).Single(tab => Name(tab) == name);

	private static string Name(AngleSharp.Dom.IElement tab) => tab.QuerySelector("span:not(.sr-only)")!.TextContent.Trim();

	private void Answers(ArrApp app, string folder)
	{
		_tester
			.TestAsync(app, Any<ArrInstanceConfig>(), Any())
			.Returns(
				Success.Of(new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p"), new ArrQualityProfile(6, "Ultra-HD")], [new ArrRootFolder(folder, 1)]))
			);
	}

	private void SonarrAnswers(params ArrQualityProfile[] profiles)
	{
		_tester
			.TestAsync(ArrApp.Sonarr, Any<ArrInstanceConfig>(), Any())
			.Returns(Success.Of(new ArrInstanceChoices(profiles, [new ArrRootFolder("/tv", 1), new ArrRootFolder("/anime", 1)])));
	}

	private static string Badge(IRenderedComponent<ArrSinkRow> cut) => cut.Find(".arr-row-head > span:last-child").TextContent.Trim();

	private static AngleSharp.Dom.IElement SwitchButton(IRenderedComponent<ArrSinkRow> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Contains("Switch the wantlist"));
	}

	private static AngleSharp.Dom.IElement StayOnPlexButton(IRenderedComponent<ArrSinkRow> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Contains("Stay on Plex"));
	}

	private IRenderedComponent<ArrSinkRow> RenderRow(bool settingUp, bool syncs = true)
	{
		return Render<ArrSinkRow>(parameters =>
			parameters
				.Add(row => row.SettingUp, settingUp)
				.Add(row => row.Syncs, syncs)
				.Add(row => row.Open, true)
				.Add(row => row.SwitchRequested, () => _switchRequests++)
				.Add(row => row.StayOnPlex, () => _stayRequests++)
				.Add(row => row.InstanceMoved, config => _moves.Add(config))
		);
	}
}
