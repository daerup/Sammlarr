using E2E.Tests.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Web.Staging;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Staging;

[ReadmeImages]
[NotInParallel]
public class ReadmeScreenshots
{
	private const string HeroFeed = "Marvel movies";

	private const string LiveRunFeed = "This season on AniList";

	private const string RatedTitle = "Project Hail Mary";

	private const int Width = 1440;

	private const int Height = 900;

	private const string EditRoute = "/feeds/edit/";

	private const string ThemeApplied =
		"dark => document.documentElement.classList.contains('dark') === dark && document.documentElement.hasAttribute('data-base-color')";

	private const string Settled = """
		() => {
			const inView = element => {
				const box = element.getBoundingClientRect();
				return box.width > 0 && box.height > 0 && box.bottom > 0 && box.right > 0 && box.top < innerHeight && box.left < innerWidth;
			};
			return ![...document.querySelectorAll('[class*="animate-pulse"]')].some(inView)
				&& [...document.images].filter(inView).every(image => image.complete);
		}
		""";

	private const string HideToasts = """
		document.addEventListener("DOMContentLoaded", () => {
			const style = document.createElement("style");
			style.textContent = "[data-testid=toasts] { display: none !important; }";
			document.head.append(style);
		});
		""";

	private static readonly TimeSpan LiveRunBudget = TimeSpan.FromMinutes(3);

	private static readonly DirectoryInfo Repo = RepoRoot(new DirectoryInfo(AppContext.BaseDirectory));

	private static readonly IConfiguration StagingFile = new ConfigurationBuilder()
		.AddJsonFile(Path.Combine(Repo.FullName, "AppHost", "appsettings.Staging.json"))
		.Build();

	private static readonly Shot[] Shots =
	[
		new(
			"feed-editor",
			page => VisitFeedAsync(page, HeroFeed, id => $"{EditRoute}{id}"),
			page => page.Locator("[data-preview-state='success']"),
			[Theme.Light, Theme.Dark]
		),
		new("home", page => page.VisitAsync(Routes.Home), page => page.Heading("Your library"), [Theme.Dark]),
		new("feeds", page => page.VisitAsync(Routes.Feeds), page => page.FeedCard(LiveRunFeed), [Theme.Dark]),
		new("feed-runs", page => VisitFeedAsync(page, LiveRunFeed, id => $"/feeds/{id}/runs"), page => page.Locator(".runs-item").First, [Theme.Dark]),
		new("wantlist", page => page.VisitAsync(Routes.Wantlist), page => Tile(page, RatedTitle), [Theme.Dark], Posters: true),
		new("browse", page => page.VisitAsync(Routes.Browse), page => page.GetByTestId("media-tile").First, [Theme.Dark], Posters: true),
		new("ratings", OpenRatingsAsync, page => page.Locator(".rating-breakdown"), [Theme.Dark], Posters: true),
		new(
			"providers",
			page => SettingsPage.VisitSectionAsync(page, "providers"),
			page => SettingsPage.MapService(page, "Radarr + Sonarr").Filter(new LocatorFilterOptions { HasText = "Connected" }),
			[Theme.Dark]
		),
		new("usage", page => SettingsPage.VisitSectionAsync(page, "usage"), page => page.Locator("[data-slot=bar-chart] svg").First, [Theme.Dark]),
	];

	private enum Theme
	{
		Light,
		Dark,
	}

	[Test]
	public async Task LiveRun_OnAFreshlyRestagedStaging_RecordsTheGif()
	{
		var frames = Directory.CreateTempSubdirectory("sammlarr-live-run-");
		try
		{
			using var playwright = await Playwright.CreateAsync();
			await using var browser = await BrowserPages.LaunchAsync(playwright);
			await using var context = await OpenAsync(browser, Theme.Dark);
			var page = await SignInAsync(context);
			await page.VisitAsync(Routes.Feeds);
			await page.WaitForFunctionAsync(ThemeApplied, true);
			await Expect(page.GetByTestId("feed-run-idle")).ToBeVisibleAsync(Wait.Settle);
			var dialog = await PickAloneAsync(page, LiveRunFeed);

			var screencast = await Screencast.StartAsync(page, frames);
			await dialog.Button("Run 1 feed").ClickAsync();
			await ParkMouseAsync(page);
			var live = page.GetByTestId("feed-run-live");
			await Expect(live).ToBeVisibleAsync(Wait.Settle);
			var start = screencast.Latest;
			var crop = await FollowAsync(live, page.Locator("#feed-run-live"), page.FeedCard(LiveRunFeed));
			await page.WaitForTimeoutAsync(2000);
			await screencast.StopAsync();

			await screencast.EncodeGifAsync(crop, start, Output("live-run", Theme.Dark, "gif"));
		}
		finally
		{
			frames.Delete(recursive: true);
		}
	}

	[Test]
	[DependsOn(nameof(LiveRun_OnAFreshlyRestagedStaging_RecordsTheGif), ProceedOnFailure = true)]
	public async Task Stills_AfterTheLiveRun_ShootEveryPageOfTheShotList()
	{
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await BrowserPages.LaunchAsync(playwright);
		foreach (var theme in Enum.GetValues<Theme>())
		{
			await using var context = await OpenAsync(browser, theme);
			var page = await SignInAsync(context);
			foreach (var shot in Shots.Where(candidate => candidate.Themes.Contains(theme)))
			{
				await shot.Open(page);
				await ShootAsync(page, shot.Name, shot.Ready(page), theme, shot.Posters);
			}
		}
	}

	[Test]
	public async Task Setup_OnAFreshStaging_ShootsTheFirstRunWizard()
	{
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await BrowserPages.LaunchAsync(playwright);
		await using var context = await OpenAsync(browser, Theme.Dark);
		var page = await context.NewPageAsync();

		await page.VisitAsync(Routes.Setup);
		Skip.Unless(
			new Uri(page.Url).AbsolutePath == Routes.Setup,
			"Staging has finished its setup, so /setup sends everyone on: reset it to the Fresh scenario and run this test alone"
		);

		await ShootAsync(page, "setup", page.Locator(".setup-card"), Theme.Dark);
	}

	private static async Task<IBrowserContext> OpenAsync(IBrowser browser, Theme theme)
	{
		var context = await browser.NewContextAsync(
			new BrowserNewContextOptions
			{
				BaseURL = $"http://{StagingFile["Staging:Host"]}:{StagingFile["Staging:Ports:Sammlarr"]}",
				ViewportSize = new ViewportSize { Width = Width, Height = Height },
				DeviceScaleFactor = 1,
				ColorScheme = Scheme(theme),
			}
		);
		await context.AddInitScriptAsync(Locators.MarkListeningInputs);
		await context.AddInitScriptAsync(HideToasts);
		await context.AddInitScriptAsync($$"""localStorage.setItem('bb-theme', JSON.stringify({ isDarkMode: {{(theme is Theme.Dark ? "true" : "false")}} }));""");
		return context;
	}

	private static async Task<IPage> SignInAsync(IBrowserContext context)
	{
		var admin = StagingFile.GetSection("Staging:Personas:Admin").Get<StagingPersona>()!;
		var page = await context.NewPageAsync();
		await page.VisitAsync(Routes.Home);
		await page.Labelled("Username").EnterAsync(admin.Name);
		await page.Labelled("Password").EnterAsync(admin.Password);
		await page.Button("Sign in").ClickAsync();
		await page.ShouldBeAtAsync(Routes.Home);
		return page;
	}

	private static async Task ShootAsync(IPage page, string name, ILocator ready, Theme theme, bool posters = false)
	{
		await page.WaitForFunctionAsync(ThemeApplied, theme is Theme.Dark);
		await Expect(ready).ToBeVisibleAsync(Wait.Preview);
		await ParkMouseAsync(page);
		await page.WaitForFunctionAsync(Settled, null, new PageWaitForFunctionOptions { Timeout = Wait.Preview, PollingInterval = 250 });
		await page.EvaluateAsync("document.fonts.ready");
		await page.WaitForTimeoutAsync(1000);
		await Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
		await page.ScreenshotAsync(
			new PageScreenshotOptions
			{
				Path = Output(name, theme, posters ? "jpg" : "png"),
				Type = posters ? ScreenshotType.Jpeg : ScreenshotType.Png,
				Quality = posters ? 85 : null,
				Animations = ScreenshotAnimations.Disabled,
			}
		);
	}

	private static Task ParkMouseAsync(IPage page) => page.Mouse.MoveAsync(Width / 2f, 24);

	private static async Task VisitFeedAsync(IPage page, string feed, Func<string, string> route)
	{
		await page.VisitAsync(Routes.Feeds);
		var edit = await page.FeedCard(feed).Locator("a.feed-card-link").GetAttributeAsync("href");
		await page.VisitAsync(route(edit![EditRoute.Length..]));
	}

	private static async Task OpenRatingsAsync(IPage page)
	{
		await page.VisitAsync(Routes.Wantlist);
		await Tile(page, RatedTitle).ClickAsync();
		var badge = page.Locator(".media-detail-sheet-content .rating-badge");
		await Expect(badge).ToBeVisibleAsync(Wait.Settle);
		await badge.ClickAsync();
	}

	private static ILocator Tile(IPage page, string title) => page.Locator("[data-testid='media-tile']", new PageLocatorOptions { HasText = title });

	private static async Task<ILocator> PickAloneAsync(IPage page, string feed)
	{
		await page.Button("Run feeds").ClickAsync();
		var dialog = page.GetByRole(AriaRole.Dialog);
		var rows = dialog.Locator(".run-row");
		await Expect(rows.First).ToBeVisibleAsync(Wait.Settle);
		foreach (var row in await rows.AllAsync())
		{
			var name = await row.Locator(".run-row-name").InnerTextAsync();
			await TickAsync(row.GetByRole(AriaRole.Checkbox), name.Trim() == feed);
		}

		await TickAsync(dialog.Locator(".run-sync").GetByRole(AriaRole.Checkbox), false);
		return dialog;
	}

	private static async Task TickAsync(ILocator box, bool ticked)
	{
		var state = ticked ? "true" : "false";
		if (await box.GetAttributeAsync("aria-checked") != state)
		{
			await box.ClickAsync();
			await Expect(box).ToHaveAttributeAsync("aria-checked", state);
		}
	}

	private static async Task<Crop> FollowAsync(ILocator live, ILocator view, ILocator card)
	{
		var crop = await MeasureAsync(view, card);
		var deadline = DateTimeOffset.UtcNow + LiveRunBudget;
		while (await live.IsVisibleAsync())
		{
			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new TimeoutException(
					$"The live run of {LiveRunFeed} did not fold within {LiveRunBudget}: restage staging and take it again, or pick a smaller feed"
				);
			}

			crop = crop.Union(await MeasureAsync(view, card));
			await Task.Delay(250);
		}

		return crop;
	}

	private static async Task<Crop> MeasureAsync(ILocator view, ILocator card)
	{
		return Crop.Of((await view.BoundingBoxAsync())!).Union(Crop.Of((await card.BoundingBoxAsync())!));
	}

	private static string Output(string name, Theme theme, string extension)
	{
		var folder = Directory.CreateDirectory(Path.Combine(Repo.FullName, "docs", "images", "readme"));
		return Path.Combine(folder.FullName, $"{name}-{theme.ToString().ToLowerInvariant()}.{extension}");
	}

	private static ColorScheme Scheme(Theme theme)
	{
		return theme switch
		{
			Theme.Light => ColorScheme.Light,
			Theme.Dark => ColorScheme.Dark,
		};
	}

	private static DirectoryInfo RepoRoot(DirectoryInfo directory)
	{
		return directory.GetFiles("Sammlarr.slnx").Length > 0 ? directory : RepoRoot(directory.Parent!);
	}

	private sealed record Shot(string Name, Func<IPage, Task> Open, Func<IPage, ILocator> Ready, Theme[] Themes, bool Posters = false);
}
