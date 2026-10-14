using Contracts.Setup;

namespace Web.Setup;

internal sealed class FirstRunRedirect(RequestDelegate next)
{
	private static readonly PathString[] Unguarded = ["/setup", "/auth", "/health", "/_framework", "/_blazor", "/_content"];

	public async Task InvokeAsync(HttpContext context)
	{
		if (IsPage(context.Request) && await context.RequestServices.GetRequiredService<IOnboardingService>().NeedsAdminAsync(context.RequestAborted))
		{
			context.Response.Redirect("/setup");
			return;
		}

		await next(context);
	}

	private static bool IsPage(HttpRequest request)
	{
		return HttpMethods.IsGet(request.Method)
			&& request.Headers.Accept.Any(accept => accept?.Contains("text/html", StringComparison.Ordinal) == true)
			&& !Unguarded.Any(path => request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
	}
}
