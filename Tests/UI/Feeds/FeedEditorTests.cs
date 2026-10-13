using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Blocklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Providers;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services;
using UI.Tests.Media;
using FeedPage = UI.Features.Feeds.Pages.Editor.Feed;

namespace UI.Tests.Feeds;

public class FeedEditorTests : BunitContext
{
	private static readonly FeedId Comedy = FeedId.From(new Guid(41, 0, 0, new byte[8]));

	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();

	private readonly Mock<IFeedProvider> _provider = IFeedProvider.Mock();

	private readonly TaskCompletionSource _listLoaded = new();

	private readonly Mock<IFeedRunPlanner> _planner = IFeedRunPlanner.Mock();

	private CancellationToken _loading;

	public FeedEditorTests()
	{
		_feeds.GetAsync(Any()).Returns(new FeedSnapshot([Overview()]));
		_feeds.UpdateAsync(Any(), Any(), Any()).Returns(Success.Of(Comedy));
		_feeds.CreateAsync(Any(), Any()).Returns(Success.Of(Comedy));
		_provider.NormalizeAsync(Any(), Any()).Returns((typed, _) => Task.FromResult<Result<string>>(Success.Of(typed)));
		_provider
			.GetFeedItemsAsync(Any(), Any(), Any())
			.Returns(
				(_, _, token) =>
				{
					_loading = token;
					return HeldAsync(_listLoaded.Task, token);
				}
			);
	}

	[Test]
	public async Task Save_WhileThePreviewLoads_ForASwitchOff_SavesAtOnce()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(cut, config => config with { IsEnabled = false });
		await SaveAsync(cut);

		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
		await Assert.That(Dialogs(cut)).IsEmpty();
	}

	[Test]
	public async Task Save_WhileThePreviewLoads_ForAFilterChange_AsksFirst()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });
		var saving = SaveAsync(cut);

		cut.WaitForState(() => Dialogs(cut).Any(dialog => dialog.TextContent.Contains("Save before the preview has finished?")));
		_feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Never);
		await ClickAsync(cut, "Save anyway");
		await saving;
		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
	}

	[Test]
	public async Task Save_WhileThePreviewLoads_ForAMirrorSwitch_AsksOnlyTheMirrorQuestionWithoutACount()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(
			cut,
			config =>
				config with
				{
					Strategy = new FeedStrategy(new Mirror(3)),
					Limit = new QueryLimit { Count = 10 },
				}
		);
		var saving = SaveAsync(cut);

		cut.WaitForState(() => Dialogs(cut).Any(dialog => dialog.TextContent.Contains(MirrorSwitch.Heading)));
		await Assert.That(Dialogs(cut).Single().TextContent).Contains("can't tell yet how many titles have already left the list");
		await ClickAsync(cut, MirrorSwitch.Confirm);
		await saving;
		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
		await Assert.That(Dialogs(cut)).IsEmpty();
	}

	[Test]
	public async Task Cancel_WhileThePreviewLoads_IsOn()
	{
		var cut = RenderEditor();

		WhileLoading(cut);

		await Assert.That(Button(cut, "Cancel").HasAttribute("disabled")).IsFalse();
	}

	[Test]
	public async Task Dispose_DuringTheListLookup_NeverLoadsTheList()
	{
		var lookup = new TaskCompletionSource<Result<string>>();
		_provider.NormalizeAsync(Any(), Any()).Returns((_, _) => lookup.Task);
		var cut = RenderEditor();
		cut.WaitForAssertion(() => _provider.NormalizeAsync(Any(), Any()).WasCalled(Times.Once));

		await DisposeComponentsAsync();
		await Renderer.Dispatcher.InvokeAsync(() => lookup.SetResult(Success.Of("hdlists/comedy")));
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		_provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Dispose_DuringTheListLookup_ShowsNoFailedToTestToast()
	{
		var lookup = new TaskCompletionSource<Result<string>>();
		_provider.NormalizeAsync(Any(), Any()).Returns((_, token) => lookup.Task.WaitAsync(token));
		var cut = RenderEditor();
		cut.WaitForAssertion(() => _provider.NormalizeAsync(Any(), Any()).WasCalled(Times.Once));

		await DisposeComponentsAsync();
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task Dispose_MidLoad_CancelsTheStreamAndShowsNoToast()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await DisposeComponentsAsync();
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		await Assert.That(_loading.IsCancellationRequested).IsTrue();
		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task Save_ForANewMirrorFeedWhileThePreviewLoads_AsksNoMirrorQuestion()
	{
		var cut = RenderEditor(edit: false);
		await ChangeAsync(
			cut,
			config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/comedy", Strategy = new FeedStrategy(new Mirror(3)) }
		);
		_ = ClickAsync(cut, "Test");
		WhileLoading(cut);

		_ = SaveAsync(cut);

		cut.WaitForState(() => Dialogs(cut).Count > 0);
		await Assert.That(Dialogs(cut).Single().TextContent).Contains("Save before the preview has finished?").And.DoesNotContain(MirrorSwitch.Heading);
	}

	[Test]
	public async Task CtrlEnter_WhileThePreviewLoads_SavesASwitchOff()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await cut.Find("[aria-busy='true'], .flex-row.gap-4").KeyDownAsync(new KeyboardEventArgs { CtrlKey = true, Key = "Enter" });

		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
	}

	[Test]
	public async Task Save_ForANewFeed_CreatesItWithTheDraftId()
	{
		var cut = RenderEditor(edit: false);
		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/comedy" });
		_ = ClickAsync(cut, "Test");
		WhileLoading(cut);

		var saving = SaveAsync(cut);
		cut.WaitForState(() => Dialogs(cut).Count > 0);
		await ClickAsync(cut, "Save anyway");
		await saving;

		cut.WaitForAssertion(() => _feeds.CreateAsync(Any(), Any()).WasCalled(Times.Once));
		var created = Mock.Invocations(_feeds).Select(call => call.Arguments[0]).OfType<FeedCreateData>().Single();
		var draft = await Assert.That(created.Id).IsSome();
		await Assert
			.That(Mock.Invocations(_planner).Select(call => call.Arguments[0]).OfType<Domain.Feed.Feed>().Select(feed => feed.Id).Distinct())
			.IsEquivalentTo([draft]);
	}

	[Test]
	public async Task Test_ForANewFeedWhoseListIsMissing_OffersRetryButNoSave()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns((identifier, _, _) => throw new FeedIdentifierNotFoundException(identifier));
		var cut = RenderEditor(edit: false);
		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/gone" });

		await ClickAsync(cut, "Test");

		cut.WaitForState(() => cut.FindAll("button").Any(button => button.TextContent.Trim() == "Retry"));
		await Assert.That(SaveButtons(cut)).IsEmpty();
	}

	[Test]
	public async Task Load_ForAnExistingFeedWhoseListIsMissing_StillOffersSaveForAChange()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns((identifier, _, _) => throw new FeedIdentifierNotFoundException(identifier));
		var cut = RenderEditor();
		cut.WaitForState(() => cut.FindAll("button").Any(button => button.TextContent.Trim() == "Retry"));

		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await Assert.That(SaveButtons(cut)).HasSingleItem();
	}

	private IReadOnlyList<ToastData> Toasts => Services.GetRequiredService<ToastService>().Toasts;

	private IRenderedComponent<IComponent> RenderEditor(bool edit = true)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		AddAuthorization().SetAuthorized("ana");
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		Services.AddSingleton(IUserScope.Mock().Object);
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(_feeds.Object);
		Services.AddSingleton<IKeyedServiceIndex<FeedProvider, IFeedProvider>>(
			new FakeKeyedServiceIndex<FeedProvider, IFeedProvider>().Register(FeedProvider.Mdblist, _provider.Object)
		);
		var readiness = IServiceReadiness.Mock();
		readiness.ReadyAsync(Any(), Any()).Returns(true);
		Services.AddSingleton(readiness.Object);
		_planner.NeedsAsync(Any(), Any()).Returns(FieldNeeds.None);
		_planner.PlanAsync(Any(), Any()).Returns(new AppendPlan([], [], FeedDelta.None, Conclusiveness.NoItems));
		Services.AddSingleton(_planner.Object);
		var wantlist = IWantlistService.Mock();
		wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(wantlist.Object);
		Services.AddSingleton(blocklist.Object);
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddScoped<ProfileDropdownService>();
		Services.AddScoped<AccountConnectionPrompt>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<BbDialogProvider>(1);
			builder.CloseComponent();
			builder.OpenComponent<FeedPage>(2);
			if (edit)
			{
				builder.AddAttribute(3, nameof(FeedPage.EditFeedId), UrlGuid.Encode(Comedy.Value));
			}

			builder.CloseComponent();
		});
	}

	private void WhileLoading(IRenderedComponent<IComponent> cut)
	{
		cut.WaitForAssertion(() => _provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once));
	}

	private static async Task ChangeAsync(IRenderedComponent<IComponent> cut, Func<FeedConfig, FeedConfig> change)
	{
		var panel = cut.FindComponent<FeedConfigPanel>();
		await cut.InvokeAsync(() => panel.Instance.OnChange.InvokeAsync(change(panel.Instance.Value)));
	}

	private static Task SaveAsync(IRenderedComponent<IComponent> cut) => SaveButtons(cut).Single().ClickAsync(new MouseEventArgs());

	private static Task ClickAsync(IRenderedComponent<IComponent> cut, string label) => Button(cut, label).ClickAsync(new MouseEventArgs());

	private static IElement Button(IRenderedComponent<IComponent> cut, string label) => cut.FindAll("button").First(button => button.TextContent.Trim() == label);

	private static List<IElement> SaveButtons(IRenderedComponent<IComponent> cut)
	{
		return [.. cut.FindAll("button").Where(button => button.TextContent.Trim() == "Save")];
	}

	private static List<IElement> Dialogs(IRenderedComponent<IComponent> cut) => [.. cut.FindAll("[role='alertdialog'], [role='dialog']")];

	private static async IAsyncEnumerable<Listed<MediaMatchResult>> HeldAsync(Task loaded, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await loaded.WaitAsync(cancellationToken);
		yield break;
	}

	private static FeedOverview Overview()
	{
		return new FeedOverview(
			new Domain.Feed.Feed
			{
				Name = "Comedy",
				Id = Comedy,
				Enabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "hdlists/comedy",
				CreatedAt = DateTimeOffset.UtcNow,
				Schedule = new FeedRunTrigger(new Manual()),
			},
			new None(),
			FeedRunHistory.None,
			0
		);
	}
}
