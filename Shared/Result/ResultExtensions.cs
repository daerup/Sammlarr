namespace Shared;

public static partial class ResultExtensions
{
	extension<T>(Result<T> result)
		where T : notnull
	{
		[GenerateAsync]
		public TResult Match<TResult>(Func<T, TResult> success, Func<Error, TResult> error) =>
			result switch
			{
				Success<T>(var value) => success(value),
				Error err => error(err),
			};

		public TResult Match<TResult>(TResult success, TResult error) =>
			result switch
			{
				Success<T> => success,
				Error => error,
			};

		[GenerateAsync]
		public Result<TResult> Map<TResult>(Func<T, TResult> map)
			where TResult : notnull =>
			result switch
			{
				Success<T>(var value) => Success.Of(map(value)),
				Error err => err,
			};

		[GenerateAsync]
		public Result<TResult> Map<TResult>(Func<T, Result<TResult>> map)
			where TResult : notnull =>
			result switch
			{
				Success<T>(var value) => map(value),
				Error err => err,
			};

		[GenerateAsync]
		public Result<TResult> Map<TResult>(Func<T, Success<TResult>> map)
			where TResult : notnull =>
			result switch
			{
				Success<T>(var value) => map(value),
				Error err => err,
			};

		public Result<T> MapError(Func<Error, Error> map) =>
			result switch
			{
				Success<T> => result,
				Error err => map(err),
			};

		[GenerateAsync]
		public Result<T> When(Func<T, bool> predicate, Error error) =>
			result switch
			{
				Success<T>(var value) => predicate(value) ? result : error,
				Error err => err,
			};

		public Result<T> When(Func<T, bool> predicate, string reason) => result.When(predicate, Error.From(reason));

		public Optional<T> ToOptional() =>
			result switch
			{
				Success<T>(var value) => Some.Of(value),
				Error => new None(),
			};

		public bool IsSuccess() => result is Success<T>;

		public bool IsError() => result is Error;

		public T GetValueOr(T fallback) =>
			result switch
			{
				Success<T>(var value) => value,
				Error => fallback,
			};

		public T GetValueOr(Func<Error, T> fallback) =>
			result switch
			{
				Success<T>(var value) => value,
				Error err => fallback(err),
			};

		public T GetOrThrow(Func<Error, Exception> onError) =>
			result switch
			{
				Success<T>(var value) => value,
				Error err => throw onError(err),
			};

		[GenerateAsync]
		public Result<T> Apply(Action<T> action)
		{
			if (result is Success<T>(var value))
			{
				action(value);
			}
			return result;
		}

		[GenerateAsync]
		public Result<T> ApplyError(Action<Error> action)
		{
			if (result is Error err)
			{
				action(err);
			}
			return result;
		}
	}
}
