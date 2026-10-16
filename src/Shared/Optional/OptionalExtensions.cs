using System.Runtime.CompilerServices;

namespace Shared;

public static partial class OptionalExtensions
{
	extension<T>(Optional<T> optional)
		where T : notnull
	{
		public bool IsSome() => optional is Some<T>;

		public bool IsNone() => optional is None;

		[GenerateAsync]
		public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) =>
			optional switch
			{
				Some<T>(var value) => some(value),
				None => none(),
			};

		[SuppressArchTest]
		[OverloadResolutionPriority(-1)]
		public async Task<TResult> Match<TResult>(Func<T, Task<TResult>> some, Func<TResult> none) =>
			optional switch
			{
				Some<T>(var value) => await some(value),
				None => none(),
			};

		[SuppressArchTest]
		[OverloadResolutionPriority(-1)]
		public async Task<TResult> Match<TResult>(Func<T, TResult> some, Func<Task<TResult>> none) =>
			optional switch
			{
				Some<T>(var value) => some(value),
				None => await none(),
			};

		public TResult Match<TResult>(TResult some, TResult none) =>
			optional switch
			{
				Some<T> => some,
				None => none,
			};

		[GenerateAsync]
		public Optional<TResult> Map<TResult>(Func<T, TResult> map)
			where TResult : notnull =>
			optional switch
			{
				Some<T>(var value) => Some.Of(map(value)),
				None => new None(),
			};

		[GenerateAsync]
		public Optional<TResult> Map<TResult>(Func<T, Optional<TResult>> map)
			where TResult : notnull =>
			optional switch
			{
				Some<T>(var value) => map(value),
				None => new None(),
			};

		[GenerateAsync]
		public Optional<TResult> Map<TResult>(Func<T, Some<TResult>> map)
			where TResult : notnull =>
			optional switch
			{
				Some<T>(var value) => map(value),
				None => new None(),
			};

		public T GetValueOr(T fallback) =>
			optional switch
			{
				Some<T>(var value) => value,
				None => fallback,
			};

		public T GetValueOr(Func<T> fallback) =>
			optional switch
			{
				Some<T>(var value) => value,
				None => fallback(),
			};

		[SuppressArchTest]
		[OverloadResolutionPriority(-1)]
		public async Task<T> GetValueOr(Func<Task<T>> fallback) =>
			optional switch
			{
				Some<T>(var value) => value,
				None => await fallback(),
			};

		public Optional<T> Or(Optional<T> fallback) =>
			optional switch
			{
				Some<T> => optional,
				None => fallback,
			};

		[GenerateAsync]
		public Optional<T> Or(Func<Optional<T>> fallback) =>
			optional switch
			{
				Some<T> => optional,
				None => fallback(),
			};

		[SuppressArchTest]
		public Optional<T> When(bool condition) => condition ? optional : new None();

		[GenerateAsync]
		public Optional<T> When(Func<T, bool> predicate) =>
			optional switch
			{
				Some<T>(var value) => predicate(value) ? optional : new None(),
				None => optional,
			};

		public Result<T> ToResult(Error error) =>
			optional switch
			{
				Some<T>(var value) => Success.Of(value),
				None => error,
			};

		public Result<T> ToResult(string reason) => optional.ToResult(Error.From(reason));

		[GenerateAsync]
		public Optional<T> Apply(Action<T> action)
		{
			if (optional is Some<T>(var value))
			{
				action(value);
			}
			return optional;
		}

		[GenerateAsync]
		public Optional<T> ApplyNone(Action action)
		{
			if (optional is None)
			{
				action();
			}
			return optional;
		}
	}
}
