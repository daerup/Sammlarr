using Contracts.Notifications;
using Domain.Notifications;
using Fluid;

namespace API.Notifications;

internal sealed class FluidTemplateRenderer : INotificationTemplateRenderer
{
	private const int MaxTemplateLength = 8 * 1024;
	private static readonly FluidParser Parser = new();
	private static readonly TemplateOptions Options = BuildOptions();

	public Result<string> Render(string template, NotificationContext context)
	{
		if (template.Length > MaxTemplateLength)
		{
			return Error.From("Notification template exceeds the maximum allowed length.");
		}
		if (!Parser.TryParse(template, out var parsed, out var parseError))
		{
			return Error.From($"Notification template is invalid: {parseError}");
		}
		try
		{
			return Success.Of(parsed.Render(new TemplateContext(context, Options)));
		}
		catch (Exception error)
		{
			return Error.From($"Notification template could not be rendered: {error.Message}");
		}
	}

	private static TemplateOptions BuildOptions()
	{
		var options = new TemplateOptions { MaxSteps = 400 };
		options.ValueConverters.Add(value => value is NotificationEventType type ? type.ToString() : null);
		options.ValueConverters.Add(value => value is TimeSpan span ? span.ToString(@"hh\:mm\:ss") : null);
		options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
		options.MemberAccessStrategy.Register<NotificationContext>();
		options.MemberAccessStrategy.Register<NotificationData>();
		options.MemberAccessStrategy.Register<NotificationMedia>();
		options.MemberAccessStrategy.Register<NotificationFeed>();
		return options;
	}
}
