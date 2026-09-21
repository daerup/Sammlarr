using Bunit;
using UI.Shared.Services;

namespace UI.Tests.Common;

public class ScrollServiceTests : BunitContext
{
	[Test]
	public async Task ToTopAfterAsync_AfterNavigating_ScrollsTheLayoutContainerInsteadOfTheWindow()
	{
		var handler = JSInterop.SetupVoid("sammlarrScroll.toTop");
		handler.SetVoidResult();
		var testee = new ScrollService(JSInterop.JSRuntime);
		var navigated = false;

		await testee.ToTopAfterAsync(() =>
		{
			navigated = true;
			return Task.CompletedTask;
		});

		await Assert.That(navigated).IsTrue();
		await Assert.That(handler.Invocations.Count).IsEqualTo(1);
	}
}
