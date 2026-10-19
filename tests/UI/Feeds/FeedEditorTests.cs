using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Bunit.TestDoubles;
using Contracts;
using Contracts.Blocklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Processing;
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

	private static readonly DateTimeOffset Now = new(2026, 10, 18, 14, 32, 0, TimeSpan.Zero);

	private static readonly TimeSpan PastThePause = TimeSpan.FromSeconds(5);

	private readonly Mock<IFeedService> _feeds = IFeedService.Mock();

	private readonly Mock<IFeedProvider> _provider = IFeedProvider.Mock();

	private readonly TaskCompletionSource _listLoaded = new();

	private readonly Mock<IFeedRunPlanner> _planner = IFeedRunPlanner.Mock();

	private readonly List<CancellationToken> _loads = [];

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
					_loads.Add(token);
					return HeldItems.UntilAsync(_listLoaded.Task, token);
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
	public async Task Save_ForAnExistingFeed_StaysInTheEditorAndSaysWhenItSaved()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await SaveAsync(cut);

		cut.WaitForState(() => Status(cut) is "saved");
		await Assert.That(Navigation.Uri).IsEqualTo(EditUri);
		await Assert.That(cut.Find("[data-save-status]").TextContent).Contains("Saved 14:32");
		await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Save_ForANewFeed_SwapsTheAddressForTheEditOne()
	{
		var cut = RenderEditor(edit: false);
		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/comedy" });
		WhileLoading(cut);

		var saving = SaveAsync(cut);
		cut.WaitForState(() => Dialogs(cut).Count > 0);
		await ClickAsync(cut, "Save anyway");
		await saving;

		cut.WaitForState(() => Navigation.Uri == EditUri);
		await Assert.That(Navigation.History.First().Options.ReplaceHistoryEntry).IsTrue();
	}

	[Test]
	public async Task Save_ForANewFeed_CreatesItWithTheDraftId()
	{
		var cut = RenderEditor(edit: false);
		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/comedy" });
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
	public async Task Save_ForANewMirrorFeedWhileThePreviewLoads_AsksNoMirrorQuestion()
	{
		var cut = RenderEditor(edit: false);
		await ChangeAsync(
			cut,
			config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/comedy", Strategy = new FeedStrategy(new Mirror(3)) }
		);
		WhileLoading(cut);

		_ = SaveAsync(cut);

		cut.WaitForState(() => Dialogs(cut).Count > 0);
		await Assert.That(Dialogs(cut).Single().TextContent).Contains("Save before the preview has finished?").And.DoesNotContain(MirrorSwitch.Heading);
	}

	[Test]
	public async Task Save_WithAChangeStillInItsPause_PlansItFirst()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		var plans = PlanCount;

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });
		await SaveAsync(cut);

		await Assert.That(PlanCount).IsEqualTo(plans + 1);
		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
	}

	[Test]
	public async Task Status_ForAChange_SaysItIsUnsaved()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await Assert.That(Status(cut)).IsEqualTo("unsaved");
		await Assert.That(cut.Find("[data-save-status]").TextContent).Contains("Unsaved changes");
	}

	[Test]
	public async Task Status_ForAClearedName_NamesTheProblemAndKeepsSaveOff()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(cut, config => config with { Name = " " });

		await Assert.That(cut.Find("[data-save-status]").TextContent).Contains("Name is required");
		await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Status_BeforeAnyChange_IsEmptyAndSaveIsOff()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await Assert.That(cut.FindAll("[data-save-status]")).IsEmpty();
		await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Close_WhileThePreviewLoads_IsOn()
	{
		var cut = RenderEditor();

		WhileLoading(cut);

		await Assert.That(Button(cut, "Close").HasAttribute("disabled")).IsFalse();
	}

	[Test]
	public async Task Close_WithoutChanges_LeavesAtOnce()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ClickAsync(cut, "Close");

		cut.WaitForState(() => Navigation.Uri == "http://localhost/feeds");
		await Assert.That(Dialogs(cut)).IsEmpty();
	}

	[Test]
	public async Task Close_WithUnsavedChanges_StaysWhenKeptEditing()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await ClickAsync(cut, "Close");
		cut.WaitForState(() => Dialogs(cut).Any(dialog => dialog.TextContent.Contains("Leave without saving?")));
		await ClickAsync(cut, "Keep editing");

		await Assert.That(Navigation.Uri).IsEqualTo(EditUri);
		_feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Close_WithUnsavedChanges_LeavesWhenDiscarded()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		await ChangeAsync(cut, config => config with { IsEnabled = false });
		var leaving = LeavingAsync();

		await ClickAsync(cut, "Close");
		cut.WaitForState(() => Dialogs(cut).Count > 0);
		await ClickAsync(cut, "Discard changes");

		await leaving.WaitAsync(DefaultWaitTimeout);
		await Assert.That(Navigation.Uri).IsEqualTo("http://localhost/feeds");
		_feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Identifier_WhileTyped_LoadsNothingUntilItIsCommitted()
	{
		var cut = RenderEditor(edit: false);

		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist });
		await ChangeAsync(cut, config => config with { Identifier = "hdlists/com" });
		await Task.Delay(TimeSpan.FromMilliseconds(500));
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Never);
		await Assert.That(cut.FindAll("[data-identifier-pending]")).HasSingleItem();

		await CommitAsync(cut);

		WhileLoading(cut);
		await Assert.That(cut.FindAll("[data-identifier-pending]")).IsEmpty();
	}

	[Test]
	public async Task Identifier_CommittedMidLoad_CancelsTheOldLoadAndStartsTheNewList()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await ChangeAsync(cut, config => config with { Identifier = "hdlists/documentaries" });
		await CommitAsync(cut);

		cut.WaitForAssertion(() => _provider.GetFeedItemsAsync("hdlists/documentaries", Any(), Any()).WasCalled(Times.Once));
		await Assert.That(_loads[0].IsCancellationRequested).IsTrue();
	}

	[Test]
	public async Task Identifier_CommittedAgainUnchanged_DoesNotLoadAgain()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await CommitAsync(cut);

		_provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Filter_ChangedOnALoadedList_ReplansAfterThePauseWithoutLoadingAgain()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		var plans = PlanCount;

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });

		await Assert.That(PlanCount).IsEqualTo(plans);
		cut.WaitForState(() => PlanCount == plans + 1, PastThePause);
		cut.WaitForState(() => Previewed(cut), PastThePause);
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Change_AfterThePlanFailed_PreviewsTheListAgain()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		_planner.PlanAsync(Any(), Any()).Returns(new FeedPlanFailed("Failed to compare two elements in the array."));
		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });
		cut.WaitForState(() => cut.FindAll("[data-preview-state='failed']").Count > 0, PastThePause);
		_planner.PlanAsync(Any(), Any()).Returns(new AppendPlan([], [], FeedDelta.None, Conclusiveness.NoItems));

		await ChangeAsync(cut, config => config with { Limit = null });

		cut.WaitForState(() => Previewed(cut), PastThePause);
	}

	[Test]
	public async Task Filter_WhileItsReplanIsPending_ShowsTheSpinnerUntilItLands()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		await Assert.That(cut.FindAll("[data-preview-busy]")).IsEmpty();

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });

		await Assert.That(cut.FindAll("[data-preview-busy]")).HasSingleItem();
		cut.WaitForState(() => Previewed(cut), PastThePause);
		await Assert.That(cut.FindAll("[data-preview-busy]")).IsEmpty();
	}

	[Test]
	public async Task Filter_ChangedTwiceWithinThePause_ReplansOnceForTheNewest()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		var plans = PlanCount;

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });
		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 20 } });

		cut.WaitForState(() => Previewed(cut), PastThePause);
		await Assert.That(PlanCount).IsEqualTo(plans + 1);
		await Assert.That(PlannedFeeds().Last().Limit.ToNullable()?.Count).IsEqualTo(20);
	}

	[Test]
	public async Task Filter_ChangedInPlaceByTheBuilder_StillReplans()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var filter = new BlazorBlueprint.Primitives.Filtering.FilterDefinition();
		filter.Conditions.Add(
			new BlazorBlueprint.Primitives.Filtering.FilterCondition
			{
				Field = nameof(MediaQueryRow.Title),
				Operator = BlazorBlueprint.Primitives.Filtering.FilterOperator.Contains,
				Value = "Derek",
			}
		);
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		await ChangeAsync(cut, config => config with { Filter = filter });
		cut.WaitForState(() => Previewed(cut), PastThePause);
		var plans = PlanCount;

		filter.Conditions[0].Value = "Ink";
		filter.Version++;
		await ChangeAsync(cut, config => config with { Filter = filter });

		cut.WaitForState(() => PlanCount == plans + 1, PastThePause);
	}

	[Test]
	public async Task Filter_ChangedMidLoad_ReplansTheTitlesAlreadyIn()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		var plans = PlanCount;

		await ChangeAsync(cut, config => config with { Limit = new QueryLimit { Count = 10 } });

		cut.WaitForState(() => PlanCount == plans + 1, PastThePause);
		await Assert.That(_loads[0].IsCancellationRequested).IsFalse();
	}

	[Test]
	public async Task Filter_WithAnUnfinishedCondition_KeepsTheLastPreview()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Movie())));
		var cut = RenderEditor();
		cut.WaitForState(() => Previewed(cut));
		var plans = PlanCount;
		var unfinished = new BlazorBlueprint.Primitives.Filtering.FilterDefinition();
		unfinished.Conditions.Add(
			new BlazorBlueprint.Primitives.Filtering.FilterCondition
			{
				Field = nameof(MediaQueryRow.MediaType),
				Operator = BlazorBlueprint.Primitives.Filtering.FilterOperator.In,
			}
		);

		await ChangeAsync(cut, config => config with { Filter = unfinished });
		await Task.Delay(TimeSpan.FromMilliseconds(800));

		await Assert.That(PlanCount).IsEqualTo(plans);
		await Assert.That(cut.Find("[data-save-status]").TextContent).Contains("Media Type condition unfinished");
	}

	[Test]
	public async Task Dispose_DuringTheListLookup_NeverLoadsTheList()
	{
		var lookup = new TaskCompletionSource<Result<string>>();
		_provider.NormalizeAsync(Any(), Any()).Returns((_, _) => lookup.Task);
		var cut = RenderEditor();
		cut.WaitForAssertion(() => _provider.NormalizeAsync(Any(), Any()).WasCalled(Times.Once));

		await DisposeFromTheDispatcherAsync();
		await Renderer.Dispatcher.InvokeAsync(() => lookup.SetResult(Success.Of("hdlists/comedy")));
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		_provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Dispose_DuringTheListLookup_CancelsItAndShowsNoFailureToast()
	{
		var lookup = new TaskCompletionSource<Result<string>>();
		var asked = CancellationToken.None;
		_provider
			.NormalizeAsync(Any(), Any())
			.Returns(
				(_, token) =>
				{
					asked = token;
					return lookup.Task.WaitAsync(token);
				}
			);
		var cut = RenderEditor();
		cut.WaitForAssertion(() => _provider.NormalizeAsync(Any(), Any()).WasCalled(Times.Once));

		await DisposeFromTheDispatcherAsync();
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		await Assert.That(asked.IsCancellationRequested).IsTrue();
		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task Dispose_MidLoad_CancelsTheStreamAndShowsNoToast()
	{
		var cut = RenderEditor();
		WhileLoading(cut);

		await DisposeFromTheDispatcherAsync();
		await Task.Delay(TimeSpan.FromMilliseconds(200));

		await Assert.That(_loads[0].IsCancellationRequested).IsTrue();
		await Assert.That(Toasts).IsEmpty();
	}

	[Test]
	public async Task CtrlEnter_WhileThePreviewLoads_SavesASwitchOff()
	{
		var cut = RenderEditor();
		WhileLoading(cut);
		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await cut.Find("[data-preview-current]").KeyDownAsync(new KeyboardEventArgs { CtrlKey = true, Key = "Enter" });

		cut.WaitForAssertion(() => _feeds.UpdateAsync(Any(), Any(), Any()).WasCalled(Times.Once));
	}

	[Test]
	public async Task Load_ForANewFeedWhoseListIsMissing_OffersRetryAndKeepsSaveOff()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns((identifier, _, _) => throw new FeedIdentifierNotFoundException(identifier));
		var cut = RenderEditor(edit: false);

		await ChangeAsync(cut, config => config with { Name = "Comedy", Provider = FeedProvider.Mdblist, Identifier = "hdlists/gone" });

		cut.WaitForState(() => cut.FindAll("button").Any(button => button.TextContent.Trim() == "Retry"));
		await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Find("[data-save-status]").TextContent).Contains("List not found");
	}

	[Test]
	public async Task Load_ForAnExistingFeedWhoseListIsMissing_StillOffersSaveForAChange()
	{
		_provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns((identifier, _, _) => throw new FeedIdentifierNotFoundException(identifier));
		var cut = RenderEditor();
		cut.WaitForState(() => cut.FindAll("button").Any(button => button.TextContent.Trim() == "Retry"));

		await ChangeAsync(cut, config => config with { IsEnabled = false });

		await Assert.That(SaveButton(cut).HasAttribute("disabled")).IsFalse();
	}

	private static string EditUri => $"http://localhost/feeds/edit/{UrlGuid.Encode(Comedy.Value)}";

	private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

	private IReadOnlyList<ToastData> Toasts => Services.GetRequiredService<ToastService>().Toasts;

	private int PlanCount => PlannedFeeds().Count;

	private List<Domain.Feed.Feed> PlannedFeeds() =>
		[.. Mock.Invocations(_planner).Select(call => call.Arguments[0]).OfType<FeedPlanTarget>().Select(Definition).WhereSome()];

	private static Optional<Domain.Feed.Feed> Definition(FeedPlanTarget target) =>
		target switch
		{
			DraftFeed(var feed, _) => Some.Of(feed),
			SavedFeed => new None(),
		};

	private Task DisposeFromTheDispatcherAsync() => Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);

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
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		clock.Zone.Getter.Returns(TimeZoneInfo.Utc);
		Services.AddSingleton(clock.Object);
		Services.AddScoped<Notifier>();
		Services.AddScoped<ProfileDropdownService>();
		Services.AddScoped<AccountConnectionPrompt>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
		Navigation.NavigateTo(edit ? EditUri : "http://localhost/feeds/create");

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
		cut.WaitForAssertion(() => _provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.AtLeastOnce));
	}

	private static bool Previewed(IRenderedComponent<IComponent> cut) =>
		cut.FindAll("[data-preview-current='true']").Count > 0 && cut.FindAll("[data-preview-state='success']").Count > 0;

	private static string? Status(IRenderedComponent<IComponent> cut) => cut.FindAll("[data-save-status]").FirstOrDefault()?.GetAttribute("data-save-status");

	private static async Task ChangeAsync(IRenderedComponent<IComponent> cut, Func<FeedConfig, FeedConfig> change)
	{
		var panel = cut.FindComponent<FeedConfigPanel>();
		await cut.InvokeAsync(() => panel.Instance.OnChange.InvokeAsync(change(panel.Instance.Value)));
	}

	private static async Task CommitAsync(IRenderedComponent<IComponent> cut)
	{
		var panel = cut.FindComponent<FeedConfigPanel>();
		await cut.InvokeAsync(() => panel.Instance.OnCommit.InvokeAsync());
	}

	private static Task SaveAsync(IRenderedComponent<IComponent> cut) => SaveButton(cut).ClickAsync(new MouseEventArgs());

	private static Task ClickAsync(IRenderedComponent<IComponent> cut, string label) => Button(cut, label).ClickAsync(new MouseEventArgs());

	private static IElement Button(IRenderedComponent<IComponent> cut, string label) => cut.FindAll("button").First(button => button.TextContent.Trim() == label);

	private static IElement SaveButton(IRenderedComponent<IComponent> cut) => Button(cut, "Save");

	private static List<IElement> Dialogs(IRenderedComponent<IComponent> cut) => [.. cut.FindAll("[role='alertdialog'], [role='dialog']")];

	private Task LeavingAsync()
	{
		var left = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Navigation.LocationChanged += (_, _) => left.TrySetResult();
		return left.Task;
	}

	private static MatchedMedia Movie()
	{
		return new MatchedMedia(
			new MediaIdentity(MediaId.New(), [TmdbId.From("movie", MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Derek vs Derek",
				ReleaseDate = new DateOnly(2027, 1, 1),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(90),
				Ratings = MediaRatings.None,
			}
		);
	}

	private static FeedOverview Overview()
	{
		return new FeedOverview(TestFeeds.Feed(Comedy.Value) with { Identifier = "hdlists/comedy" }, new None(), FeedRunHistory.None, 0);
	}
}
