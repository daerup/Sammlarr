using Internal.Generated.WolverineHandlers;
using Messaging;

namespace Architecture.Tests;

public class GeneratedHandlersTests
{
	private const string Regenerate = "stop the app, then Remove-Item -Recurse Messaging/Internal/Generated; dotnet run --project Web -- codegen write";

	private static readonly GeneratedHandlerRegistry Testee = new();

	[Test]
	public async Task HandlerTypes_ListEveryHandlerInMessaging_SoNoneIsLeftOutOfTheGeneratedCode()
	{
		var handlers = typeof(MessagingModule).Assembly.GetExportedTypes().Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal));

		await Assert.That(Testee.HandlerTypes()).IsEquivalentTo(handlers).Because(Regenerate);
	}
}
