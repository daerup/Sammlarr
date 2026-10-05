using BlazorBlueprint.Components;
using Bunit;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Feed;
using Domain.Identity;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers;

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

	private readonly Mock<IArrSinkConfigService> _configs = IArrSinkConfigService.Mock();

	private readonly Mock<IArrConnectionTester> _tester = IArrConnectionTester.Mock();

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	private static readonly User Guest = new(TestCurrentUser.DefaultId, "Guest", string.Empty, UserRole.Admin, DateTimeOffset.UnixEpoch);

	private int _switchRequests;

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
	public async Task Row_WhenBothAppsAnswer_SaysConnectedWithWhereEachAdds()
	{
		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Connected");
		await Assert.That(Badge(cut)).IsEqualTo("Connected");
		await Assert.That(cut.Markup).Contains("Radarr: HD-1080p into /movies · Sonarr: HD-1080p into /tv");
	}

	[Test]
	public async Task Row_WhenSonarrRefusesTheKey_NamesItOnTheClosedRow()
	{
		_tester.TestAsync(ArrApp.Sonarr, Any<ArrInstanceConfig>(), Any()).Returns(Error.From("Sonarr answered 401 Unauthorized at http://sonarr:8989."));

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => Badge(cut) == "Sonarr not answering");
		await Assert.That(Badge(cut)).IsEqualTo("Sonarr not answering");
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
	public async Task Test_WhenNothingIsChosenYet_StoresTheAppsFirstProfileAndFolder()
	{
		_configs.GetAsync(Any()).Returns(Connected with { Radarr = Connected.Radarr with { QualityProfileId = new None(), RootFolderPath = new None() } });

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
	public async Task Tags_ForAPersonRenamedSinceTheyJoined_PreviewTheNameTheyJoinedWith()
	{
		_users.GetAllAsync(Any()).Returns([Guest with { DisplayName = "Renamed" }]);

		var cut = RenderRow(settingUp: false);

		cut.WaitForState(() => cut.FindComponents<ArrTagsSection>().Count == 1);
		await Assert.That(cut.FindComponent<ArrTagsSection>().Instance.Users).IsEquivalentTo([new ArrTagUser(TestCurrentUser.DefaultId.Value, "Guest")]);
	}

	[Test]
	[Arguments(MediaProvider.Radarr, true)]
	[Arguments(MediaProvider.Plex, false)]
	public async Task Row_WhenRadarrIsRinged_DrawsTheRing(MediaProvider ringed, bool drawn)
	{
		var cut = Render<CascadingValue<ProviderRowKey?>>(parameters =>
			parameters
				.Add(value => value.Value, ringed)
				.Add(value => value.Name, ProviderRows.Ringed)
				.AddChildContent<ArrSinkRow>(row => row.Add(arr => arr.SettingUp, false).Add(arr => arr.Open, true))
		);

		await Assert.That(cut.Find(".arr-row").ClassList.Contains("is-ringed")).IsEqualTo(drawn);
	}

	private void Answers(ArrApp app, string folder)
	{
		_tester
			.TestAsync(app, Any<ArrInstanceConfig>(), Any())
			.Returns(
				Success.Of(new ArrInstanceChoices([new ArrQualityProfile(4, "HD-1080p"), new ArrQualityProfile(6, "Ultra-HD")], [new ArrRootFolder(folder, 1)]))
			);
	}

	private static string Badge(IRenderedComponent<ArrSinkRow> cut) => cut.Find(".arr-row-head > span:last-child").TextContent.Trim();

	private static AngleSharp.Dom.IElement SwitchButton(IRenderedComponent<ArrSinkRow> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Contains("Switch the wantlist"));
	}

	private IRenderedComponent<ArrSinkRow> RenderRow(bool settingUp)
	{
		return Render<ArrSinkRow>(parameters =>
			parameters
				.Add(row => row.SettingUp, settingUp)
				.Add(row => row.Open, true)
				.Add(row => row.SwitchRequested, () => _switchRequests++)
				.Add(row => row.InstanceMoved, config => _moves.Add(config))
		);
	}
}
