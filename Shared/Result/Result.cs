namespace Shared;

public sealed record Error
{
	public string Reason { get; }
	public Optional<Exception> Exception { get; }

	private Error(string reason, Optional<Exception> exception)
	{
		Reason = reason;
		Exception = exception;
	}

	public static Error From(string reason) => new(reason, new None());

	public static Error From(Exception exception) => new(exception.Message, Some.Of(exception));

	public static Error From(string reason, Exception exception) => new(reason, Some.Of(exception));

	public Optional<TException> CausedBy<TException>()
		where TException : Exception
	{
		return Exception.When(exception => exception is TException).Map(exception => (TException)exception);
	}
}

public record Success<T>(T Value) where T : notnull
{
	public static implicit operator Success<T>(T value) => new(value);
}

public static class Success
{
	public static Success<T> Of<T>(T value) where T : notnull => new(value);
}

public static class Result
{
	public static Result<T> WhenNotNull<T>(T? value, Error error) where T : class => value is not null ? Success.Of(value) : error;

	public static Result<T> WhenNotNull<T>(T? value, string reason) where T : class => WhenNotNull(value, Error.From(reason));

	[SuppressArchTest]
	public static async Task<Result<T>> WhenNotNull<T>(Task<T?> value, string reason) where T : class => WhenNotNull(await value, reason);
}

public union Result<T>(Success<T>, Error) where T : notnull;
