using Bunit;
using Microsoft.AspNetCore.Components;

namespace UI.Tests;

internal static class VirtualizeViewport
{
	private const string Init = "Blazor._internal.Virtualize.init";

	extension(BunitContext context)
	{
		internal void MeasureVirtualizeByHand()
		{
			context.JSInterop.SetupVoid(Init, _ => true).SetVoidResult();
			context.JSInterop.SetupVoid("Blazor._internal.Virtualize.restoreAnchor", _ => true);
			context.JSInterop.SetupVoid("Blazor._internal.Virtualize.refreshObservers", _ => true).SetVoidResult();
		}

		internal Task ReportViewportAsync<TComponent>(IRenderedComponent<TComponent> cut)
			where TComponent : IComponent
		{
			const int viewportFill = 2;
			var reference = context.JSInterop.Invocations[Init][^1].Arguments[0]!;
			var virtualize = reference.GetType().GetProperty("Value")!.GetValue(reference)!;
			var report = virtualize.GetType().GetMethod("OnSpacerBeforeVisible")!;
			return cut.InvokeAsync(() => report.Invoke(virtualize, [0f, 0f, 800f, viewportFill]));
		}
	}
}
