using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class PaginationControlsTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenThereIsMoreThanOnePage_FollowsThePillWithTheDockSentinel()
	{
		var cut = Render(totalPages: 4);

		await Assert.That(cut.Find(".pager-float + .pager-end")).IsNotNull();
	}

	[Test]
	public async Task Render_WhenEverythingFitsOnOnePage_ShowsNeitherThePillNorTheSentinel()
	{
		var cut = Render(totalPages: 1);

		await Assert.That(cut.FindAll(".pager-pill")).IsEmpty();
		await Assert.That(cut.FindAll(".pager-end")).IsEmpty();
	}

	[Test]
	public async Task Phone_ReachingTheEnd_AsksForMore()
	{
		var reached = 0;
		var cut = this.RenderOnPhone<PaginationControls>(parameters =>
			parameters.Add(pager => pager.TotalPages, 4).Add(pager => pager.OnReachEnd, () => reached++)
		).Component;

		await cut.InvokeAsync(() => cut.Instance.OnEndInView(true));

		await Assert.That(reached).IsEqualTo(1);
	}

	[Test]
	public async Task Phone_ReachingTheEndOfTheLastPage_AsksForNothing()
	{
		var reached = 0;
		var cut = this.RenderOnPhone<PaginationControls>(parameters =>
			parameters.Add(pager => pager.CurrentPage, 4).Add(pager => pager.TotalPages, 4).Add(pager => pager.OnReachEnd, () => reached++)
		).Component;

		await cut.InvokeAsync(() => cut.Instance.OnEndInView(true));

		await Assert.That(reached).IsEqualTo(0);
	}

	[Test]
	public async Task Desktop_ReachingTheEnd_OnlyDocksThePill()
	{
		var reached = 0;
		var cut = Render<PaginationControls>(parameters => parameters.Add(pager => pager.TotalPages, 4).Add(pager => pager.OnReachEnd, () => reached++));

		await cut.InvokeAsync(() => cut.Instance.OnEndInView(true));

		await Assert.That(reached).IsEqualTo(0);
		await Assert.That(cut.Find(".pager-pill").ClassList).Contains("is-docked");
	}

	[Test]
	public async Task Dispose_WhileItsModuleIsStillLoading_ReleasesItOnceItArrives()
	{
		var import = new TaskCompletionSource<IJSObjectReference>();
		var js = IJSRuntime.Mock();
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Is<object?[]?>(args => args![0] is "./js/pager.js"))
			.Returns((_, _) => new ValueTask<IJSObjectReference>(import.Task));
		Services.AddSingleton(js.Object);
		var module = IJSObjectReference.Mock();
		module
			.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>())
			.Throws(new ObjectDisposedException("DotNetObjectReference`1[[UI.Shared.Common.PaginationControls]]"));
		Render(totalPages: 4);

		await Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);
		await Renderer.Dispatcher.InvokeAsync(() => import.SetResult(module.Object));
		await SettledAsync();

		module.DisposeAsync().WasCalled(Times.Once);
		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Watch_OvertakenByANewerOne_StopsOnlyTheStaleWatcher()
	{
		var started = new TaskCompletionSource<IJSObjectReference>();
		var js = IJSRuntime.Mock();
		var module = IJSObjectReference.Mock();
		var stale = IJSObjectReference.Mock();
		var fresh = IJSObjectReference.Mock();
		var watches = 0;
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>()).Returns(module.Object);
		module
			.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>())
			.Returns((_, _) => watches++ == 0 ? new ValueTask<IJSObjectReference>(started.Task) : new ValueTask<IJSObjectReference>(fresh.Object));
		Services.AddSingleton(js.Object);
		var cut = Render(totalPages: 4);

		cut.Render(parameters => parameters.Add(pager => pager.TotalPages, 1));
		cut.Render(parameters => parameters.Add(pager => pager.TotalPages, 4));
		await Renderer.Dispatcher.InvokeAsync(() => started.SetResult(stale.Object));
		await SettledAsync();

		stale.DisposeAsync().WasCalled(Times.Once);
		fresh.DisposeAsync().WasCalled(Times.Never);
	}

	[Test]
	public async Task Watch_AfterTheImportTimedOut_ImportsAgain()
	{
		var js = IJSRuntime.Mock();
		var imports = 0;
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Is<object?[]?>(args => args![0] is "./js/pager.js"))
			.Returns(
				(_, _) =>
					imports++ == 0
						? ValueTask.FromException<IJSObjectReference>(new TaskCanceledException())
						: new ValueTask<IJSObjectReference>(IJSObjectReference.Mock().Object)
			);
		Services.AddSingleton(js.Object);
		var cut = Render(totalPages: 4);

		cut.Render(parameters => parameters.Add(pager => pager.TotalPages, 1));
		cut.Render(parameters => parameters.Add(pager => pager.TotalPages, 4));

		await Assert.That(imports).IsEqualTo(2);
		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Watch_WhenTheCircuitIsGone_ReportsNoError()
	{
		var js = IJSRuntime.Mock();
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>()).Throws(new JSDisconnectedException("gone"));
		Services.AddSingleton(js.Object);

		Render(totalPages: 4);

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task Dispose_WhenTheWatcherIsAlreadyGone_ReportsNoError()
	{
		var js = IJSRuntime.Mock();
		var module = IJSObjectReference.Mock();
		var watcher = IJSObjectReference.Mock();
		js.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>()).Returns(module.Object);
		module.InvokeAsync<IJSObjectReference>(Any<string>(), Any<object?[]?>()).Returns(watcher.Object);
		watcher.DisposeAsync().Throws(new ObjectDisposedException("JSObjectReference"));
		Services.AddSingleton(js.Object);
		Render(totalPages: 4);

		await Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	private Task SettledAsync() => Renderer.Dispatcher.InvokeAsync(() => { });

	private IRenderedComponent<PaginationControls> Render(int totalPages) =>
		Render<PaginationControls>(parameters => parameters.Add(pager => pager.TotalPages, totalPages));
}
