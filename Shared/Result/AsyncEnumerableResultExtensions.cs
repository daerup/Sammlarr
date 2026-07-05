namespace Shared;

public static class AsyncEnumerableResultExtensions
{
	public static async IAsyncEnumerable<T> WhereSuccess<T>(this IAsyncEnumerable<Result<T>> source)
		where T : notnull
	{
		await foreach (var result in source)
		{
			if (result is Success<T>(var value))
			{
				yield return value;
			}
		}
	}

	public static async IAsyncEnumerable<(T Value, TCompanion Companion)> WhereSuccess<T, TCompanion>(
		this IAsyncEnumerable<(Result<T> Result, TCompanion Companion)> source
	)
		where T : notnull
	{
		await foreach (var (result, companion) in source)
		{
			if (result is Success<T>(var value))
			{
				yield return (value, companion);
			}
		}
	}
}
