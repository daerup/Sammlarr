using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class VirtualizeGuardTests : BunitContext
{
	private readonly TaskCompletionSource _afterRender = new();

	[Test]
	public async Task AfterRender_WhenVirtualizeLosesItsJsReferenceWhileDisposing_DoesNotCrashTheCircuit()
	{
		await FailAfterDisposalAsync(
			new ObjectDisposedException("Microsoft.JSInterop.DotNetObjectReference`1[[Microsoft.AspNetCore.Components.Web.Virtualization.VirtualizeJsInterop]]")
		);

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsFalse();
	}

	[Test]
	public async Task AfterRender_WhenAnythingElseFails_StillCrashesTheCircuit()
	{
		var failure = new ObjectDisposedException(nameof(SemaphoreSlim));

		await FailAfterDisposalAsync(failure);

		await Assert.That(Renderer.UnhandledException.IsCompleted).IsTrue();
		await Assert.That(await Renderer.UnhandledException).IsSameReferenceAs(failure);
	}

	private async Task FailAfterDisposalAsync(Exception failure)
	{
		Render<VirtualizeGuard>(parameters =>
			parameters.AddChildContent<PendingAfterRender>(child => child.Add(pending => pending.AfterRender, _afterRender.Task))
		);

		await Renderer.Dispatcher.InvokeAsync(DisposeComponentsAsync);
		await Renderer.Dispatcher.InvokeAsync(() => _afterRender.SetException(failure));
	}

	private sealed class PendingAfterRender : ComponentBase
	{
		[Parameter]
		public required Task AfterRender { get; set; }

		protected override async Task OnAfterRenderAsync(bool firstRender) => await AfterRender;
	}
}
