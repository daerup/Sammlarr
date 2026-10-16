using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Shared.DI;

namespace Web.RateLimiting;

internal sealed class RateLimitingModule : IServiceModule
{
	internal const string SignInForms = "sign-in-forms";

	internal const int RequestBurst = 300;

	internal const int SignInFormsPerMinute = 10;

	internal const string TooManyRequests = "Too many requests from here. Wait a minute and try again.";

	public void RegisterServices(IServiceCollection services)
	{
		services.AddRateLimiter(options =>
		{
			options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
				RateLimitPartition.GetTokenBucketLimiter(
					Caller(context),
					_ => new TokenBucketRateLimiterOptions
					{
						TokenLimit = RequestBurst,
						TokensPerPeriod = 50,
						ReplenishmentPeriod = TimeSpan.FromSeconds(10),
					}
				)
			);
			options.AddPolicy(
				SignInForms,
				context =>
					RateLimitPartition.GetFixedWindowLimiter(
						Address(context),
						_ => new FixedWindowRateLimiterOptions { PermitLimit = SignInFormsPerMinute, Window = TimeSpan.FromMinutes(1) }
					)
			);
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			options.OnRejected = async (context, cancellationToken) =>
			{
				if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
				{
					context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
				}

				await context.HttpContext.Response.WriteAsync(TooManyRequests, cancellationToken);
			};
		});
	}

	private static string Caller(HttpContext context)
	{
		return context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } user ? $"user:{user}" : $"address:{Address(context)}";
	}

	private static string Address(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}
