using Microsoft.JSInterop;

namespace UI.Shared.Services;

internal class ScrollService(IJSRuntime jsRuntime)
{
	public async Task ToTopAfterAsync(Func<Task> navigateAction, CancellationToken cancellationToken = default)
	{
		var task = navigateAction();
		await Task.Delay(50, cancellationToken);
		await jsRuntime.InvokeVoidAsync("scrollTo", cancellationToken, new { top = 0, behavior = "smooth" });
		await task;
	}
}
