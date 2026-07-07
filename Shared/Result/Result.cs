namespace Shared;

public readonly record struct Error
{
	public string Reason { get; }
	public Exception? Exception { get; }

	private Error(string reason, Exception? exception)
	{
		Reason = reason;
		Exception = exception;
	}

	public static Error From(string reason) => new(reason, null);

	public static Error From(Exception exception) => new(exception.Message, exception);
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

	public static async Task<Result<T>> WhenNotNull<T>(Task<T?> value, Error error) where T : class => WhenNotNull(await value, error);

	public static async Task<Result<T>> WhenNotNull<T>(Task<T?> value, string reason) where T : class => WhenNotNull(await value, reason);
}

public union Result<T>(Success<T>, Error) where T : notnull;
