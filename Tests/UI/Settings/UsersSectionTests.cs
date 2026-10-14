using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Users;
using UI.Shared.Branding.Birds;
using UI.Shared.Services;

namespace UI.Tests.Settings;

public class UsersSectionTests : BunitContext
{
	private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

	private static readonly User Admin = new(TestCurrentUser.DefaultId, "guest", string.Empty, UserRole.Admin, Now.AddDays(-25));

	private static readonly User Member = new(UserId.New(), "daerup", string.Empty, UserRole.Member, Now.AddDays(-18));

	private static readonly DateTimeOffset LockedUntil = Now.AddMinutes(10);

	private static readonly Person[] People =
	[
		new JoinedPerson(Admin, new PersonHoldings(3, 15, 2, []), new None(), new StoredColour(Some.Of(40d), new None())),
		new JoinedPerson(Member, PersonHoldings.Empty, Some.Of(LockedUntil), new StoredColour(Some.Of(177.5d), new None())),
		new InvitedPerson("mira", "mira", Now.AddDays(6).AddHours(2)),
	];

	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	private readonly Mock<IProviderConfigService> _config = IProviderConfigService.Mock();

	private readonly Mock<IPictureGrids> _pictures = IPictureGrids.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		Services.AddSingleton(clock.Object);
		_users.GetPeopleAsync(Any()).Returns(People);
		Services.AddSingleton(_users.Object);
		Services.AddSingleton(_config.Object);
		_pictures.ReadAsync(Any(), Any()).Returns(new None());
		Services.AddSingleton(_pictures.Object);
		Services.AddScoped<PersonPainter>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(RegistrationMode.Open, "true")]
	[Arguments(RegistrationMode.Closed, "false")]
	public async Task Render_OnAPlexInstall_ShowsWhetherNewSignUpsAreAllowed(RegistrationMode mode, string allowed)
	{
		Installed(AuthProvider.Plex, mode);

		var cut = Render<UsersSection>();

		await Assert.That(cut.WaitForElement("[aria-label='Allow new sign-ups']").GetAttribute("aria-checked")).IsEqualTo(allowed);
		await Assert.That(cut.FindAll("button").Any(button => button.TextContent.Contains("Invite member"))).IsFalse();
	}

	[Test]
	public async Task Render_OnASammlarrAccountsInstall_OffersInvitesAndDescribesEachPerson()
	{
		Installed(AuthProvider.Local);

		var cut = Render<UsersSection>();

		cut.WaitForElement(".person-row");
		await Assert.That(cut.FindAll("[aria-label='Allow new sign-ups']")).IsEmpty();
		await Assert.That(cut.FindAll("button").Any(button => button.TextContent.Contains("Invite member"))).IsTrue();
		var rows = cut.FindAll(".person-row")
			.Select(row => $"{row.QuerySelector("[data-owns]")!.TextContent.Trim()} | {row.QuerySelector("[data-status]")!.TextContent.Trim()}");
		await Assert
			.That(rows)
			.IsEquivalentTo([
				"3 feeds · 15 wanted · 2 blocklisted | Active",
				$"No feeds or titles yet | Locked until {LockedUntil.ToLocalTime():HH:mm}",
				"Not joined yet | Invited · link expires in 6 days",
			]);
		var birds = cut.FindAll(".person-row").Select(row => $"{row.GetAttribute("style")} | {row.QuerySelectorAll("[data-bird]").Length}");
		await Assert.That(birds).IsEquivalentTo(["--bird-hue: 40 | 1", "--bird-hue: 177.5 | 1", " | 0"]);
	}

	[Test]
	public async Task Rows_ForInvitesRunningOut_SayHowLongIsLeftOrThatTheyExpired()
	{
		Installed(AuthProvider.Local);
		_users
			.GetPeopleAsync(Any())
			.Returns([
				new JoinedPerson(Admin, PersonHoldings.Empty, new None(), new StoredColour(Some.Of(40d), new None())),
				new InvitedPerson("soon", "soon", Now.AddHours(5)),
				new InvitedPerson("old", "old", Now.AddHours(-1)),
			]);

		var cut = Render<UsersSection>();

		cut.WaitForElement(".person-row");
		var statuses = cut.FindAll("[data-status]").Select(status => status.TextContent.Trim());
		await Assert.That(statuses).IsEquivalentTo(["Active", "Invited · link expires in 5 hours", "Invite expired"]);
	}

	[Test]
	public async Task Rows_ForSomeoneWithAPictureColour_CarryItsChromaToTheEdge()
	{
		Installed(AuthProvider.Plex);
		_users
			.GetPeopleAsync(Any())
			.Returns([
				new JoinedPerson(
					Admin,
					PersonHoldings.Empty,
					new None(),
					new StoredColour(Some.Of(40d), Some.Of(new StoredPicture("https://plex.tv/users/guest/avatar", Some.Of(new PictureTone(90.7, 0.042)))))
				),
			]);

		var cut = Render<UsersSection>();

		await Assert.That(cut.WaitForElement(".person-row").GetAttribute("style")).IsEqualTo("--bird-hue: 90.7; --bird-chroma: 0.042; --bird-scale: 0.35");
	}

	[Test]
	public async Task Rows_ForSomeoneWithoutAColourYet_GiveThemTheNextGoldenHueAndStoreIt()
	{
		Installed(AuthProvider.Local);
		_users
			.GetPeopleAsync(Any())
			.Returns([
				new JoinedPerson(Admin, PersonHoldings.Empty, new None(), new StoredColour(Some.Of(40d), new None())),
				new JoinedPerson(Member, PersonHoldings.Empty, new None(), StoredColour.Empty),
			]);

		var cut = Render<UsersSection>();

		cut.WaitForAssertion(() =>
			_users.StoreColourAsync(Member.Id, Is<StoredColour>(colour => colour!.GoldenHue.GetValueOr(0) == 177.5), Any()).WasCalled(Times.Once)
		);
		await Assert.That(cut.Find("[data-person='daerup']").GetAttribute("style")).IsEqualTo("--bird-hue: 177.5");
	}

	[Test]
	public async Task Rows_WhileAPictureIsStillBeingRead_ShowWithTheColourTheyHave()
	{
		Installed(AuthProvider.Plex);
		_pictures.ReadAsync(Any(), Any()).ReturnsAsync(new TaskCompletionSource<Optional<byte[]>>().Task);
		_users
			.GetPeopleAsync(Any())
			.Returns([
				new JoinedPerson(
					Admin with
					{
						AvatarUrl = "https://plex.tv/users/guest/avatar",
					},
					PersonHoldings.Empty,
					new None(),
					new StoredColour(Some.Of(40d), new None())
				),
			]);

		var cut = Render<UsersSection>();

		await Assert.That(cut.WaitForElement(".person-row").GetAttribute("style")).IsEqualTo("--bird-hue: 40");
	}

	[Test]
	public async Task Rows_OnAPlexInstall_ShowTheirPlexPictureWithTheBirdBehindIt()
	{
		Installed(AuthProvider.Plex);
		_users
			.GetPeopleAsync(Any())
			.Returns([
				new JoinedPerson(
					Admin with
					{
						AvatarUrl = "https://plex.tv/users/guest/avatar",
					},
					PersonHoldings.Empty,
					new None(),
					new StoredColour(Some.Of(40d), new None())
				),
			]);

		var cut = Render<UsersSection>();

		await Assert.That(cut.WaitForElement(".person-row img").GetAttribute("src")).IsEqualTo("https://plex.tv/users/guest/avatar");
		await Assert.That(cut.FindAll(".person-row [data-bird]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Rows_OnAPlexInstall_ShowTheBirdForSomeoneWithoutAPlexPicture()
	{
		Installed(AuthProvider.Plex);
		_users.GetPeopleAsync(Any()).Returns([new JoinedPerson(Admin, PersonHoldings.Empty, new None(), new StoredColour(Some.Of(40d), new None()))]);

		var cut = Render<UsersSection>();

		cut.WaitForElement(".person-row [data-bird]");
		await Assert.That(cut.FindAll(".person-row img")).IsEmpty();
	}

	[Test]
	public async Task OpenRow_ForTheLastAdmin_SaysWhyTheyCannotBeDemotedOrRemoved()
	{
		Installed(AuthProvider.Local);

		var row = await OpenRowAsync("guest");

		await Assert.That(row.TextContent).Contains("The last admin can't be made a member or removed.");
		await Assert.That(Buttons(row)).DoesNotContain("Remove");
	}

	[Test]
	public async Task OpenRow_ForALockedMemberOnASammlarrAccountsInstall_OffersEveryAction()
	{
		Installed(AuthProvider.Local);

		var row = await OpenRowAsync("daerup");

		await Assert.That(Buttons(row)).IsEquivalentTo(["Unlock", "Reset link", "Make admin", "Remove"]);
	}

	[Test]
	public async Task OpenRow_ForAMemberOnAPlexInstall_HasNoResetLink()
	{
		Installed(AuthProvider.Plex);

		var row = await OpenRowAsync("daerup");

		await Assert.That(Buttons(row)).DoesNotContain("Reset link");
	}

	[Test]
	public async Task OpenRow_ForAnInvite_OffersANewLinkOrRevokingIt()
	{
		Installed(AuthProvider.Local);

		var row = await OpenRowAsync("mira");

		await Assert.That(Buttons(row)).IsEquivalentTo(["New invite link", "Revoke invite"]);
	}

	private void Installed(AuthProvider signIn, RegistrationMode registration = RegistrationMode.Open)
	{
		_config
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = signIn,
					RegistrationMode = registration,
				}
			);
	}

	private async Task<IElement> OpenRowAsync(string name)
	{
		var cut = Render<UsersSection>();
		cut.WaitForElement(".person-row");
		await cut.Find($"[data-person='{name}'] button").ClickAsync();
		return cut.WaitForElement($"[data-person='{name}']");
	}

	private static IReadOnlyList<string> Buttons(IElement row)
	{
		return [.. row.QuerySelectorAll("button").Skip(1).Select(button => button.TextContent.Trim())];
	}
}
