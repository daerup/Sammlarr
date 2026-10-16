using BlazorBlueprint.Primitives.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class KeyboardShortcutTests : BunitContext
{
	[Test]
	public async Task Render_WhenTheCircuitDisposesTheShortcutServiceMidRegistration_DoesNotCrashTheCircuit()
	{
		var shortcuts = IKeyboardShortcutService.Mock();
		shortcuts.RegisterAsync(Any<string>(), Any<Func<Task>>(), Any<string>()).Throws(new ObjectDisposedException(nameof(SemaphoreSlim)));
		Services.AddSingleton(shortcuts.Object);

		var render = () => Render<KeyboardShortcut>(parameters => parameters.Add(shortcut => shortcut.Keys, "Ctrl+K"));

		await Assert.That(render).ThrowsNothing();
	}
}
