using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Shared.Telemetry;

namespace UI.Telemetry;

public class BlazorHubActivityFilter : IHubFilter
{
	private readonly BlazorRouteTracker _routes = new();

	public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
	{
		var connectionId = invocationContext.Context.ConnectionId;
		var url = _routes.UpdateAndGet(connectionId, invocationContext.HubMethodName, invocationContext.HubMethodArguments);
		if (Activity.Current is { } current)
		{
			if (url is not null)
			{
				var label = BlazorRouteTracker.GetFriendlyLabel(invocationContext.HubMethodName);
				current.DisplayName = label is not null ? $"{label} {url}" : url;
			}
			SammlarrTelemetry.TagCircuitId(current, connectionId);
		}
		using var activity = SammlarrTelemetry.StartBlazorHubInvocation(invocationContext.HubMethodName, connectionId);
		try
		{
			return await next(invocationContext);
		}
		catch (Exception ex)
		{
			activity?.AddException(ex);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
	{
		_routes.Remove(context.Context.ConnectionId);
		return next(context, exception);
	}
}
