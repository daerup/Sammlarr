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
	public async Task Dispose_WhileItsModuleIsStillLoading_ReportsNoError()
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

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	private IRenderedComponent<PaginationControls> Render(int totalPages) =>
		Render<PaginationControls>(parameters => parameters.Add(pager => pager.TotalPages, totalPages));
}
