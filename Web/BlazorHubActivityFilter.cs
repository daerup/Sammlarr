using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Observability.Tracing;
using UI.Observability;

namespace Web;

internal sealed class BlazorHubActivityFilter : IHubFilter
{
	private readonly BlazorRouteTracker _routes = new();

	public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
	{
		var connectionId = invocationContext.Context.ConnectionId;
		var url = _routes.UpdateAndGet(connectionId, invocationContext.HubMethodName, invocationContext.HubMethodArguments);
		var label = BlazorRouteTracker.GetFriendlyLabel(invocationContext.HubMethodName);
		if (label is null)
		{
			return await next(invocationContext);
		}

		using var activity = Instrumentation.Source.StartActivity($"{label} {url ?? invocationContext.HubMethodName}");
		activity?.SetTag("circuit.id", connectionId);
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
