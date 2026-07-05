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

public union Result<T>(T, Error) where T : notnull;
