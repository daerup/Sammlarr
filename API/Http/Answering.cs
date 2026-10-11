using Domain.Auth;
using Domain.Providers;
using Polly.Timeout;
using Shared.Concurrency;

namespace API.Http;

internal static class Answering
{
	extension<T>(Task<Result<T>> asked)
		where T : notnull
	{
		public async Task<Answer<T>> GuardedAsync(RemoteService remote)
		{
			try
			{
				return Of(remote, await asked);
			}
			catch (Exception exception) when (exception is not OperationCanceledException || exception.InnerException is TimeoutException)
			{
				return Of<T>(remote, exception, Error.From(exception));
			}
		}
	}

	private static Answer<T> Of<T>(RemoteService remote, Result<T> result)
		where T : notnull
	{
		return result.Match<T, Answer<T>>(found => new Found<T>(found), error => Of<T>(remote, error));
	}

	private static Answer<T> Of<T>(RemoteService remote, Error error)
		where T : notnull
	{
		return error.Exception.Match<Exception, Answer<T>>(exception => Of<T>(remote, exception, error), () => new NoMatch(remote));
	}

	private static Answer<T> Of<T>(RemoteService remote, Exception exception, Error error)
		where T : notnull
	{
		return exception switch
		{
			RemoteUnavailableException unavailable => new Unavailable(remote, unavailable.Until, unavailable.Sent),
			HttpRequestException or TimeoutRejectedException => new Unavailable(remote, new None(), true),
			OperationCanceledException { InnerException: TimeoutException } => new Unavailable(remote, new None(), true),
			LaneRefusedException or SecretRejectedException => new Unavailable(remote, new None(), false),
			_ => new Faulted(remote, error),
		};
	}
}
