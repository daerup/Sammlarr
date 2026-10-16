using Microsoft.Extensions.Logging;

namespace Messaging;

public sealed class PingHandler
{
	public static void Handle(Ping message, ILogger<PingHandler> logger) => logger.LogInformation("Ping received: {Text}", message.Text);
}
