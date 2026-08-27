using Domain.Notifications;

namespace Observability.Logging;

internal static class SensitiveModel
{
	public static IReadOnlyDictionary<Type, IReadOnlySet<string>> Build() =>
		new SensitiveModelBuilder()
			.Sensitive<WebhookConfig>(config => config.SigningSecret)
			.Sensitive<PushoverConfig>(config => config.AppToken)
			.Sensitive<PushoverConfig>(config => config.UserKey)
			.Build();
}
